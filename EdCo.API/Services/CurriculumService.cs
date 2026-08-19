using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EdCo.API.DTOs;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace EdCo.API.Services
{
    /// <summary>
    /// Domain service encapsulating all student-facing curriculum operations.
    /// Extracted from the 593-line CurriculumController to enforce SRP.
    /// </summary>
    public class CurriculumService : ICurriculumService
    {
        private readonly EdCoDbContext _context;
        private readonly EdCo.Core.Interfaces.ICacheService _cacheService;

        public CurriculumService(EdCoDbContext context, EdCo.Core.Interfaces.ICacheService cacheService)
        {
            _context = context;
            _cacheService = cacheService;
        }

        public async Task<int> GetStudentGradeLevelIdAsync(string? userId, string? gradeLevelIdClaim, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                if (user != null && user.GradeLevelId.HasValue && user.GradeLevelId.Value > 0)
                {
                    return user.GradeLevelId.Value;
                }
            }

            if (int.TryParse(gradeLevelIdClaim, out int parsedClaimId) && parsedClaimId > 0)
            {
                return parsedClaimId;
            }

            var firstGrade = await _context.GradeLevels.OrderBy(g => g.Id).FirstOrDefaultAsync(cancellationToken);
            if (firstGrade != null)
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = await _context.Users.FindAsync(new object[] { userId }, cancellationToken);
                    if (user != null && (!user.GradeLevelId.HasValue || user.GradeLevelId.Value <= 0))
                    {
                        user.GradeLevelId = firstGrade.Id;
                        await _context.SaveChangesAsync(cancellationToken);
                    }
                }
                return firstGrade.Id;
            }

            return 1;
        }

        public async Task<List<SubjectDto>> GetSubjectsAsync(int gradeLevelId, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"Curriculum:Subjects:{gradeLevelId}";
            return await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                return await _context.Subjects
                    .Where(s => s.GradeLevelId == gradeLevelId)
                    .Select(s => new SubjectDto
                    {
                        Id = s.Id,
                        Name = s.Name,
                        GradeLevelId = s.GradeLevelId
                    })
                    .ToListAsync(cancellationToken);
            }, TimeSpan.FromMinutes(30));
        }

        public async Task<List<ChapterManifestDto>?> GetSubjectManifestAsync(int subjectId, int studentGradeId, CancellationToken cancellationToken = default)
        {
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId, cancellationToken);
            if (subject == null || subject.GradeLevelId != studentGradeId)
                return null;

            var cacheKey = $"Curriculum:Manifest:{subjectId}";
            var chapters = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                return await _context.Chapters
                    .Where(c => c.SubjectId == subjectId)
                    .OrderBy(c => c.OrderIndex)
                    .Select(c => new ChapterManifestDto
                    {
                        Id = c.Id,
                        Title = c.Title,
                        OrderIndex = c.OrderIndex,
                        Units = c.Units.OrderBy(u => u.OrderIndex).Select(u => new UnitManifestDto
                        {
                            Id = u.Id,
                            Title = u.Title,
                            OrderIndex = u.OrderIndex,
                            VideoId = u.Video != null ? u.Video.Id : null,
                            NotesId = u.Notes != null ? u.Notes.Id : null,
                            QuizId = u.Quiz != null ? u.Quiz.Id : null
                        }).ToList()
                    })
                    .ToListAsync(cancellationToken);
            }, TimeSpan.FromMinutes(30));

            return chapters;
        }

        public async Task<(bool Success, string? ErrorMessage, object? Result)> GetSubjectExamsAsync(int subjectId, int studentGradeId, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var subject = await _context.Subjects.FirstOrDefaultAsync(s => s.Id == subjectId, cancellationToken);
            if (subject == null || subject.GradeLevelId != studentGradeId)
                return (false, "Subject not found or not available for your grade level.", null);

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.Quizzes.Where(q => q.SubjectId == subjectId && q.IsExam);
            var totalCount = await query.CountAsync(cancellationToken);

            var exams = await query
                .OrderBy(q => q.DisplayPosition)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(q => new
                {
                    Id = q.Id,
                    Title = q.Title,
                    DisplayPosition = q.DisplayPosition,
                    QuestionCount = q.Questions.Count
                })
                .ToListAsync(cancellationToken);

            return (true, null, new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                data = exams
            });
        }

        public async Task<(bool Success, string? ErrorMessage, object? Result)> GetQuizDetailsAsync(int quizId, int studentGradeId, string? userId, CancellationToken cancellationToken = default)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Subject)
                .Include(q => q.Unit!)
                    .ThenInclude(u => u.Chapter!)
                        .ThenInclude(c => c.Subject)
                .Include(q => q.Questions)
                .FirstOrDefaultAsync(q => q.Id == quizId, cancellationToken);

            if (quiz == null)
                return (false, "Quiz not found.", null);

            int quizGradeLevelId = quiz.Subject?.GradeLevelId
                ?? quiz.Unit?.Chapter?.Subject?.GradeLevelId
                ?? 0;

            if (quizGradeLevelId != 0 && quizGradeLevelId != studentGradeId)
                return (false, "Quiz not available for your grade level.", null);

            var attemptedQuestionIds = new HashSet<int>();
            if (!string.IsNullOrEmpty(userId))
            {
                attemptedQuestionIds = await _context.QuizQuestionAttempts
                    .Where(a => a.AppUserId == userId)
                    .Select(a => a.QuizQuestionId)
                    .ToHashSetAsync(cancellationToken);
            }

            var result = new
            {
                quiz.Id,
                quiz.IsExam,
                Title = string.IsNullOrEmpty(quiz.Title) ? "Quiz" : quiz.Title,
                Questions = quiz.Questions.Select(q => new
                {
                    q.Id,
                    q.QuestionText,
                    q.ImageUrl,
                    QuestionType = q.QuestionType.ToString(),
                    q.Points,
                    Options = q.QuestionType == QuestionType.MultipleChoice
                        ? new[] { q.OptionA, q.OptionB, q.OptionC, q.OptionD }.Where(o => !string.IsNullOrEmpty(o))
                        : null,
                    CorrectAnswer = q.QuestionType == QuestionType.MultipleChoice
                        ? (q.CorrectOption == "A" ? q.OptionA : q.CorrectOption == "B" ? q.OptionB : q.CorrectOption == "C" ? q.OptionC : q.OptionD)
                        : q.CorrectAnswer,
                    IsAttempted = attemptedQuestionIds.Contains(q.Id)
                })
            };

            return (true, null, result);
        }

        public async Task<(bool Success, bool RequiresSubscription, string? ErrorMessage, UnitDetailsDto? Result)> GetUnitDetailsAsync(int unitId, int studentGradeId, string? userId, string baseUrl, CancellationToken cancellationToken = default)
        {
            var user = await _context.Users.FindAsync(new object[] { userId! }, cancellationToken);
            bool isSubscribed = user != null && user.IsSubscribed && user.SubscriptionEndDate >= DateTime.UtcNow;

            if (!isSubscribed)
                return (false, true, "Subscription required.", null);

            var unit = await _context.Units
                .Include(u => u.Chapter!)
                    .ThenInclude(c => c.Subject)
                .Include(u => u.Video)
                .Include(u => u.Notes)
                .Include(u => u.Quiz!)
                    .ThenInclude(q => q.Questions)
                .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);

            if (unit == null || unit.Chapter?.Subject?.GradeLevelId != studentGradeId)
                return (false, false, "Unit not found or not available for your grade level.", null);

            var attemptedQuestionIds = new HashSet<int>();
            if (!string.IsNullOrEmpty(userId))
            {
                attemptedQuestionIds = await _context.QuizQuestionAttempts
                    .Where(a => a.AppUserId == userId)
                    .Select(a => a.QuizQuestionId)
                    .ToHashSetAsync(cancellationToken);
            }

            var random = new Random();
            var randomQuestions = unit.Quiz?.Questions
                .Where(q => !attemptedQuestionIds.Contains(q.Id))
                .OrderBy(q => random.Next())
                .Take(5)
                .Select(q => new QuizQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    ImageUrl = q.ImageUrl,
                    QuestionType = q.QuestionType.ToString(),
                    Points = q.Points,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectOption = q.CorrectOption
                })
                .ToList() ?? new List<QuizQuestionDto>();

            var dto = new UnitDetailsDto
            {
                Id = unit.Id,
                Title = unit.Title,
                ChapterTitle = unit.Chapter?.Title ?? string.Empty,
                VideoUrl = unit.Video?.EncryptedStreamUrl != null
                    ? (unit.Video.EncryptedStreamUrl.StartsWith("http") ? unit.Video.EncryptedStreamUrl : $"{baseUrl}{(unit.Video.EncryptedStreamUrl.StartsWith("/") ? "" : "/")}{unit.Video.EncryptedStreamUrl}")
                    : null,
                NotesUrl = unit.Notes?.DocumentUrl != null
                    ? (unit.Notes.DocumentUrl.StartsWith("http") ? unit.Notes.DocumentUrl : $"{baseUrl}{(unit.Notes.DocumentUrl.StartsWith("/") ? "" : "/")}{unit.Notes.DocumentUrl}")
                    : null,
                NotesMarkdown = !string.IsNullOrWhiteSpace(unit.Notes?.MarkdownBlob)
                    ? unit.Notes?.MarkdownBlob
                    : unit.Notes?.ExtractedDocumentText,
                Questions = randomQuestions
            };

            return (true, false, null, dto);
        }

        public async Task<(bool Success, string? ErrorMessage, List<QuizQuestionDto>? Result)> GetOfflineQuestionsAsync(int unitId, int studentGradeId, CancellationToken cancellationToken = default)
        {
            var unit = await _context.Units
                .Include(u => u.Chapter!)
                    .ThenInclude(c => c.Subject)
                .Include(u => u.Quiz!)
                    .ThenInclude(q => q.Questions)
                .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);

            if (unit == null || unit.Chapter?.Subject?.GradeLevelId != studentGradeId)
                return (false, "Unit not found or not available for your grade level.", null);

            var mcqQuestions = unit.Quiz?.Questions
                .Where(q => q.QuestionType == QuestionType.MultipleChoice)
                .Select(q => new QuizQuestionDto
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    ImageUrl = q.ImageUrl,
                    QuestionType = q.QuestionType.ToString(),
                    Points = q.Points,
                    OptionA = q.OptionA,
                    OptionB = q.OptionB,
                    OptionC = q.OptionC,
                    OptionD = q.OptionD,
                    CorrectOption = q.CorrectOption
                })
                .ToList() ?? new List<QuizQuestionDto>();

            return (true, null, mcqQuestions);
        }

        public async Task<(bool Success, string? ErrorMessage, object? Result)> GetFlashcardsAsync(int unitId, int studentGradeId, string? userId, CancellationToken cancellationToken = default)
        {
            var unit = await _context.Units
                .Include(u => u.Chapter!)
                    .ThenInclude(c => c.Subject)
                .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);

            if (unit == null || unit.Chapter?.Subject?.GradeLevelId != studentGradeId)
                return (false, "Unit not found or not available for your grade level.", null);

            var allFlashcards = await _context.Flashcards
                .Where(f => f.UnitId == unitId)
                .Select(f => new
                {
                    f.Id,
                    f.FrontContent,
                    f.BackContent
                })
                .ToListAsync(cancellationToken);

            var masteredIds = new HashSet<int>();
            if (!string.IsNullOrEmpty(userId))
            {
                masteredIds = await _context.StudentFlashcardProgresses
                    .Where(p => p.AppUserId == userId && p.IsMastered)
                    .Select(p => p.FlashcardId)
                    .ToHashSetAsync(cancellationToken);
            }

            var result = new
            {
                active = allFlashcards.Where(f => !masteredIds.Contains(f.Id)).ToList(),
                mastered = allFlashcards.Where(f => masteredIds.Contains(f.Id)).ToList()
            };

            return (true, null, result);
        }

        public async Task<bool> MasterFlashcardAsync(string userId, int flashcardId, CancellationToken cancellationToken = default)
        {
            var progress = await _context.StudentFlashcardProgresses
                .FirstOrDefaultAsync(p => p.AppUserId == userId && p.FlashcardId == flashcardId, cancellationToken);

            if (progress == null)
            {
                progress = new StudentFlashcardProgress
                {
                    AppUserId = userId,
                    FlashcardId = flashcardId,
                    IsMastered = true,
                    MasteredAt = DateTime.UtcNow
                };
                _context.StudentFlashcardProgresses.Add(progress);
            }
            else
            {
                progress.IsMastered = true;
                progress.MasteredAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task SubmitQuizAttemptsAsync(string userId, List<QuizAttemptDto> attempts, CancellationToken cancellationToken = default)
        {
            foreach (var attempt in attempts)
            {
                _context.QuizQuestionAttempts.Add(new QuizQuestionAttempt
                {
                    AppUserId = userId,
                    QuizQuestionId = attempt.QuestionId,
                    IsCorrect = attempt.IsCorrect,
                    AttemptedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<object> GetPerformanceAsync(string userId, int studentGradeId, CancellationToken cancellationToken = default)
        {
            var unitPerformance = await _context.QuizQuestionAttempts
                .Where(a => a.AppUserId == userId
                            && a.QuizQuestion.Quiz.Unit != null
                            && a.QuizQuestion.Quiz.Unit.Chapter.Subject != null
                            && a.QuizQuestion.Quiz.Unit.Chapter.Subject.GradeLevelId == studentGradeId)
                .GroupBy(a => new
                {
                    UnitId = a.QuizQuestion.Quiz.Unit!.Id,
                    UnitTitle = a.QuizQuestion.Quiz.Unit.Title,
                    SubjectName = a.QuizQuestion.Quiz.Unit.Chapter.Subject!.Name
                })
                .Select(g => new
                {
                    UnitId = g.Key.UnitId,
                    UnitTitle = g.Key.UnitTitle,
                    SubjectName = g.Key.SubjectName,
                    TotalAttempts = g.Count(),
                    Correct = g.Count(a => a.IsCorrect)
                })
                .ToListAsync(cancellationToken);

            var rawSubjectGroups = await _context.QuizQuestionAttempts
                .Where(a => a.AppUserId == userId)
                .Select(a => new
                {
                    SubjectId = a.QuizQuestion.Quiz.Unit != null
                        ? a.QuizQuestion.Quiz.Unit.Chapter.SubjectId
                        : (a.QuizQuestion.Quiz.SubjectId ?? 0),
                    IsCorrect = a.IsCorrect
                })
                .Where(x => x.SubjectId != 0)
                .GroupBy(x => x.SubjectId)
                .Select(g => new
                {
                    SubjectId = g.Key,
                    TotalAttempts = g.Count(),
                    Correct = g.Count(x => x.IsCorrect)
                })
                .ToListAsync(cancellationToken);

            var validSubjectIds = await _context.Subjects
                .Where(s => s.GradeLevelId == studentGradeId)
                .Select(s => s.Id)
                .ToListAsync(cancellationToken);

            var filteredSubjectGroups = rawSubjectGroups
                .Where(g => validSubjectIds.Contains(g.SubjectId))
                .ToList();

            var subjectNames = await _context.Subjects
                .Where(s => validSubjectIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);

            var subjectPerformance = filteredSubjectGroups.Select(g => new
            {
                SubjectId = g.SubjectId,
                SubjectName = subjectNames.TryGetValue(g.SubjectId, out var name) ? name : "Unknown",
                TotalAttempts = g.TotalAttempts,
                Correct = g.Correct
            }).ToList();

            return new { unitPerformance, subjectPerformance };
        }

        public async Task<(bool Success, string? ErrorMessage)> ResetPerformanceAsync(string userId, int? unitId, int? subjectId, CancellationToken cancellationToken = default)
        {
            var query = _context.QuizQuestionAttempts
                .Include(a => a.QuizQuestion)
                    .ThenInclude(q => q.Quiz)
                        .ThenInclude(q => q.Unit!)
                            .ThenInclude(u => u.Chapter!)
                                .ThenInclude(c => c.Subject!)
                .Where(a => a.AppUserId == userId);

            if (unitId.HasValue)
            {
                query = query.Where(a => a.QuizQuestion.Quiz.UnitId == unitId.Value);
            }
            else if (subjectId.HasValue)
            {
                query = query.Where(a => a.QuizQuestion.Quiz.Unit != null && a.QuizQuestion.Quiz.Unit.Chapter != null && a.QuizQuestion.Quiz.Unit.Chapter.SubjectId == subjectId.Value);
            }
            else
            {
                return (false, "Must provide UnitId or SubjectId.");
            }

            var toRemove = await query.ToListAsync(cancellationToken);
            _context.QuizQuestionAttempts.RemoveRange(toRemove);
            await _context.SaveChangesAsync(cancellationToken);

            return (true, null);
        }
    }
}
