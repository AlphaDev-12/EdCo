using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EdCo.API.DTOs;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Exceptions;
using EdCo.Core.Interfaces;

namespace EdCo.API.Services
{
    public class AiGradingService : IAiGradingService
    {
        private readonly EdCoDbContext _context;
        private readonly IGeminiVisionService _visionService;
        private readonly IAiCreditGuardService _creditGuard;
        private readonly IGradingJobQueue _jobQueue;
        private readonly IErrorLogService _errorLogService;
        private readonly IAiGradingPromptBuilder _promptBuilder;
        private readonly IAiResponseParserService _parserService;
        private readonly ILogger<AiGradingService> _logger;

        public AiGradingService(
            EdCoDbContext context,
            IGeminiVisionService visionService,
            IAiCreditGuardService creditGuard,
            IGradingJobQueue jobQueue,
            IErrorLogService errorLogService,
            IAiGradingPromptBuilder promptBuilder,
            IAiResponseParserService parserService,
            ILogger<AiGradingService> logger)
        {
            _context = context;
            _visionService = visionService;
            _creditGuard = creditGuard;
            _jobQueue = jobQueue;
            _errorLogService = errorLogService;
            _promptBuilder = promptBuilder;
            _parserService = parserService;
            _logger = logger;
        }

        public async Task<(bool Success, int StatusCode, string? ErrorMessage, AiGradeResponseDto? Result)> GradeQuestionAsync(AiGradeRequestDto request, string? userId)
        {
            const decimal estCost = 0.03m;
            if (!string.IsNullOrEmpty(userId))
            {
                var (allowed, errorMsg) = await _creditGuard.ReserveHoldingCreditAsync(userId, estCost);
                if (!allowed)
                {
                    return (false, 402, errorMsg, null);
                }
            }

            try
            {
                var question = await _context.QuizQuestions.FirstOrDefaultAsync(q => q.Id == request.QuestionId);
                if (question == null) return (false, 444, "Question not found.", null); // 444 custom code for NotFound mapping

                if (question.QuestionType == QuestionType.MultipleChoice || question.QuestionType == QuestionType.TrueFalse)
                {
                    return (false, 400, "This endpoint is only for AI-graded questions.", null);
                }

                var systemContext = _promptBuilder.BuildQuestionGradingPrompt(question, request.StudentAnswer);

                string replyMessage;
                var questionImages = new List<string>();
                if (!string.IsNullOrWhiteSpace(question.ImageUrl))
                {
                    var qBase64 = question.ImageUrl.Contains(",") ? question.ImageUrl.Split(',')[1] : question.ImageUrl;
                    questionImages.Add(qBase64);
                }
                if (!string.IsNullOrWhiteSpace(question.CorrectAnswerImageUrl))
                {
                    var refBase64 = question.CorrectAnswerImageUrl.Contains(",") ? question.CorrectAnswerImageUrl.Split(',')[1] : question.CorrectAnswerImageUrl;
                    questionImages.Add(refBase64);
                }

                if (questionImages.Count > 0)
                {
                    var visionPrompt = systemContext;
                    if (!string.IsNullOrWhiteSpace(question.CorrectAnswerImageUrl))
                    {
                        visionPrompt += "\n\nNote: An official Reference Solution Diagram is attached as an image. Use it alongside the reference answer text to grade the student's response.";
                    }

                    try
                    {
                        if (questionImages.Count == 1)
                        {
                            replyMessage = await _visionService.ExtractMathFromImageAsync(questionImages[0], visionPrompt, userId);
                        }
                        else
                        {
                            replyMessage = await _visionService.ExtractMathFromImagesAsync(questionImages, visionPrompt, userId);
                        }
                    }
                    catch (GroqRateLimitException grEx)
                    {
                        _logger.LogWarning(grEx, "Vision AI rate limited for question {QuestionId}", request.QuestionId);
                        await _errorLogService.LogErrorAsync(grEx, source: "AiTutor", logLevel: "Warning");
                        return (false, 429, grEx.Message, null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Vision AI grading failed for question {QuestionId}", request.QuestionId);
                        await _errorLogService.LogErrorAsync(ex, source: "AiTutor", logLevel: "Error");
                        return (false, 500, "Failed to reach vision AI provider.", null);
                    }
                }
                else
                {
                    try
                    {
                        replyMessage = await _visionService.GenerateContentAsync(systemContext, userId);
                    }
                    catch (GroqRateLimitException grEx)
                    {
                        _logger.LogWarning(grEx, "Text AI rate limited for question {QuestionId}", request.QuestionId);
                        await _errorLogService.LogErrorAsync(grEx, source: "AiTutor", logLevel: "Warning");
                        return (false, 429, grEx.Message, null);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Text AI grading failed for question {QuestionId}", request.QuestionId);
                        await _errorLogService.LogErrorAsync(ex, source: "AiTutor", logLevel: "Error");
                        return (false, 500, "Failed to reach AI provider.", null);
                    }
                }

                if (string.IsNullOrWhiteSpace(replyMessage))
                {
                    return (false, 500, "AI returned an empty response.", null);
                }

                var gradeResponse = _parserService.CleanAndParseGradeResponse(replyMessage);
                if (gradeResponse == null)
                {
                    _logger.LogError("Failed to parse AI grading JSON. Raw response: {Response}", replyMessage);
                    return (false, 500, "Failed to parse AI grading JSON.", null);
                }

                if (gradeResponse.PointsAwarded > question.Points) gradeResponse.PointsAwarded = question.Points;
                if (gradeResponse.PointsAwarded < 0) gradeResponse.PointsAwarded = 0;

                if (!string.IsNullOrWhiteSpace(gradeResponse.CriteriaBreakdown))
                {
                    _logger.LogInformation("Grading criteria breakdown for User {UserId}, Question {QuestionId}: {CriteriaBreakdown}",
                        userId ?? "Unknown", request.QuestionId, gradeResponse.CriteriaBreakdown);
                }

                return (true, 200, null, gradeResponse);
            }
            finally
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    await _creditGuard.ReleaseHoldingCreditAsync(userId, estCost);
                }
            }
        }

        public async Task<(bool Success, int StatusCode, string? ErrorMessage, AiGradeResponseDto? Result)> GradeQuestionImageAsync(AiGradeImageRequestDto request, string? userId)
        {
            const decimal estCost = 0.05m;
            if (!string.IsNullOrEmpty(userId))
            {
                var (allowed, errorMsg) = await _creditGuard.ReserveHoldingCreditAsync(userId, estCost);
                if (!allowed)
                {
                    return (false, 402, errorMsg, null);
                }
            }

            try
            {
                var question = await _context.QuizQuestions.FirstOrDefaultAsync(q => q.Id == request.QuestionId);
                if (question == null) return (false, 444, "Question not found.", null);

                if (question.QuestionType == QuestionType.MultipleChoice || question.QuestionType == QuestionType.TrueFalse)
                {
                    return (false, 400, "Image grading is only for short-answer and essay questions.", null);
                }

                var (fullGradingPrompt, allImages) = _promptBuilder.BuildImageGradingPrompt(question, request.Base64Image, request.Base64Images);
                if (allImages.Count == 0)
                {
                    return (false, 400, "No image provided.", null);
                }

                string aiReply;
                try
                {
                    if (allImages.Count == 1)
                    {
                        aiReply = await _visionService.ExtractMathFromImageAsync(allImages[0], fullGradingPrompt, userId);
                    }
                    else
                    {
                        aiReply = await _visionService.ExtractMathFromImagesAsync(allImages, fullGradingPrompt, userId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Vision AI grading failed for question {QuestionId}", request.QuestionId);
                    return (false, 500, "Failed to reach vision AI provider.", null);
                }

                if (string.IsNullOrWhiteSpace(aiReply))
                {
                    return (false, 500, "Vision AI returned an empty response.", null);
                }

                var gradeResponse = _parserService.CleanAndParseGradeResponse(aiReply);
                if (gradeResponse == null)
                {
                    _logger.LogError("Failed to parse vision AI grading JSON. Raw response: {Response}", aiReply);
                    return (false, 500, "Failed to parse vision AI grading response.", null);
                }

                if (gradeResponse.PointsAwarded > question.Points) gradeResponse.PointsAwarded = question.Points;
                if (gradeResponse.PointsAwarded < 0) gradeResponse.PointsAwarded = 0;

                if (!string.IsNullOrWhiteSpace(gradeResponse.CriteriaBreakdown))
                {
                    _logger.LogInformation("Grading criteria breakdown for User {UserId}, Question {QuestionId}: {CriteriaBreakdown}",
                        userId ?? "Unknown", request.QuestionId, gradeResponse.CriteriaBreakdown);
                }

                return (true, 200, null, gradeResponse);
            }
            finally
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    await _creditGuard.ReleaseHoldingCreditAsync(userId, estCost);
                }
            }
        }

        public async Task<(bool Success, int StatusCode, string? ErrorMessage, AiBatchGradeResponseDto? Result)> GradeQuizBatchAsync(AiBatchGradeRequestDto request, string? userId)
        {
            if (request == null || request.Questions == null || request.Questions.Count == 0)
            {
                return (false, 400, "No questions provided for batch grading.", null);
            }

            decimal estCost = Math.Max(0.02m, request.Questions.Count * 0.015m);

            if (!string.IsNullOrEmpty(userId))
            {
                var (allowed, errorMsg) = await _creditGuard.ReserveHoldingCreditAsync(userId, estCost);
                if (!allowed)
                {
                    return (false, 402, errorMsg, null);
                }
            }

            try
            {
                var questionIds = request.Questions.Select(q => q.QuestionId).ToList();
                var questions = await _context.QuizQuestions
                    .Where(q => questionIds.Contains(q.Id))
                    .ToDictionaryAsync(q => q.Id);

                var batchPayload = new List<object>();
                foreach (var req in request.Questions)
                {
                    if (questions.TryGetValue(req.QuestionId, out var q))
                    {
                        batchPayload.Add(new
                        {
                            QuestionId = q.Id,
                            QuestionText = q.QuestionText,
                            MaxPoints = q.Points,
                            ReferenceAnswer = q.CorrectAnswer ?? "N/A",
                            Rubric = q.RubricJson ?? "[]",
                            StudentAnswer = req.StudentAnswer
                        });
                    }
                }

                if (batchPayload.Count == 0)
                {
                    return (false, 400, "No valid questions found.", null);
                }

                var prompt = $@"# Role
You are an expert K-12 AI Grader. Grade the following array of student question responses against their criteria and rubrics.

# Questions & Responses:
{JsonSerializer.Serialize(batchPayload)}

OUTPUT FORMAT:
Respond ONLY with a raw JSON object:
{{
  ""GradedQuestions"": [
    {{
      ""QuestionId"": 101,
      ""PointsAwarded"": integer,
      ""CriteriaBreakdown"": ""string"",
      ""Feedback"": ""string""
    }}
  ]
}}
Do not output markdown code blocks or commentary.";

                var replyMessage = await _visionService.GenerateContentAsync(prompt, userId);
                if (string.IsNullOrWhiteSpace(replyMessage))
                {
                    return (false, 500, "AI returned an empty response.", null);
                }

                var cleaned = _parserService.CleanJsonResponse(replyMessage);
                using var doc = JsonDocument.Parse(cleaned);
                var results = new List<AiGradeResponseDtoWithQuestion>();

                if (doc.RootElement.TryGetProperty("GradedQuestions", out var arrayProp))
                {
                    foreach (var elem in arrayProp.EnumerateArray())
                    {
                        int qId = elem.GetProperty("QuestionId").GetInt32();
                        int pts = elem.GetProperty("PointsAwarded").GetInt32();
                        string bd = elem.TryGetProperty("CriteriaBreakdown", out var b) ? b.GetString() ?? "" : "";
                        string fb = elem.TryGetProperty("Feedback", out var f) ? f.GetString() ?? "" : "";

                        if (questions.TryGetValue(qId, out var q))
                        {
                            pts = Math.Clamp(pts, 0, q.Points);
                        }

                        results.Add(new AiGradeResponseDtoWithQuestion
                        {
                            QuestionId = qId,
                            PointsAwarded = pts,
                            CriteriaBreakdown = bd,
                            Feedback = fb
                        });
                    }
                }

                return (true, 200, null, new AiBatchGradeResponseDto
                {
                    Success = true,
                    GradedQuestions = results
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch quiz grading failed.");
                return (false, 500, "Failed to perform batch AI grading.", null);
            }
            finally
            {
                if (!string.IsNullOrEmpty(userId))
                {
                    await _creditGuard.ReleaseHoldingCreditAsync(userId, estCost);
                }
            }
        }

        public async Task<(bool Success, int StatusCode, string? ErrorMessage, QuizJobStatusResponseDto? Result)> SubmitQuizAsync(QuizSubmissionRequestDto request, string userId)
        {
            if (request == null || request.Answers == null || request.Answers.Count == 0)
            {
                return (false, 400, "No answers provided in quiz submission.", null);
            }

            decimal estCost = Math.Max(0.05m, request.Answers.Count * 0.03m);

            if (userId != "anonymous")
            {
                var (allowed, errorMsg) = await _creditGuard.ReserveHoldingCreditAsync(userId, estCost);
                if (!allowed)
                {
                    return (false, 402, errorMsg, null);
                }
            }

            try
            {
                var job = new GradingJobItem
                {
                    JobId = Guid.NewGuid().ToString(),
                    StudentUserId = userId,
                    QuizId = request.QuizId,
                    Questions = request.Answers,
                    TotalQuestions = request.Answers.Count,
                    Status = "Enqueued",
                    CreatedAt = DateTime.UtcNow
                };

                await _jobQueue.EnqueueJobAsync(job);

                return (true, 202, null, new QuizJobStatusResponseDto
                {
                    JobId = job.JobId,
                    Status = job.Status,
                    TotalQuestions = job.TotalQuestions,
                    CompletedQuestions = 0,
                    Results = new List<AiGradeResponseDtoWithQuestion>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue quiz submission job.");
                return (false, 500, "Failed to accept quiz submission.", null);
            }
        }

        public async Task<QuizJobStatusResponseDto?> GetQuizJobStatusAsync(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId)) return null;

            var job = await _jobQueue.GetJobStatusAsync(jobId);
            if (job == null) return null;

            return new QuizJobStatusResponseDto
            {
                JobId = job.JobId,
                Status = job.Status,
                TotalQuestions = job.TotalQuestions,
                CompletedQuestions = job.CompletedQuestions,
                Results = job.Results,
                ErrorMessage = job.ErrorMessage
            };
        }
    }
}
