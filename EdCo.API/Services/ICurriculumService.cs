using System.Collections.Generic;
using System.Threading.Tasks;
using EdCo.API.DTOs;

using System.Threading;

namespace EdCo.API.Services
{
    /// <summary>
    /// Domain service interface for all student-facing curriculum operations.
    /// Encapsulates data access, caching, and business logic previously in CurriculumController.
    /// </summary>
    public interface ICurriculumService
    {
        Task<int> GetStudentGradeLevelIdAsync(string? userId, string? gradeLevelIdClaim, CancellationToken cancellationToken = default);
        Task<List<SubjectDto>> GetSubjectsAsync(int gradeLevelId, CancellationToken cancellationToken = default);
        Task<List<ChapterManifestDto>?> GetSubjectManifestAsync(int subjectId, int studentGradeId, CancellationToken cancellationToken = default);
        Task<(bool Success, string? ErrorMessage, object? Result)> GetSubjectExamsAsync(int subjectId, int studentGradeId, int page, int pageSize, CancellationToken cancellationToken = default);
        Task<(bool Success, string? ErrorMessage, object? Result)> GetQuizDetailsAsync(int quizId, int studentGradeId, string? userId, CancellationToken cancellationToken = default);
        Task<(bool Success, bool RequiresSubscription, string? ErrorMessage, UnitDetailsDto? Result)> GetUnitDetailsAsync(int unitId, int studentGradeId, string? userId, string baseUrl, CancellationToken cancellationToken = default);
        Task<(bool Success, string? ErrorMessage, List<QuizQuestionDto>? Result)> GetOfflineQuestionsAsync(int unitId, int studentGradeId, CancellationToken cancellationToken = default);
        Task<(bool Success, string? ErrorMessage, object? Result)> GetFlashcardsAsync(int unitId, int studentGradeId, string? userId, CancellationToken cancellationToken = default);
        Task<bool> MasterFlashcardAsync(string userId, int flashcardId, CancellationToken cancellationToken = default);
        Task SubmitQuizAttemptsAsync(string userId, List<QuizAttemptDto> attempts, CancellationToken cancellationToken = default);
        Task<object> GetPerformanceAsync(string userId, int studentGradeId, CancellationToken cancellationToken = default);
        Task<(bool Success, string? ErrorMessage)> ResetPerformanceAsync(string userId, int? unitId, int? subjectId, CancellationToken cancellationToken = default);
    }

    public class QuizAttemptDto
    {
        public int QuestionId { get; set; }
        public bool IsCorrect { get; set; }
    }
}
