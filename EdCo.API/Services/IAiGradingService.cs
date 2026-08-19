using EdCo.API.DTOs;

namespace EdCo.API.Services
{
    public interface IAiGradingService
    {
        Task<(bool Success, int StatusCode, string? ErrorMessage, AiGradeResponseDto? Result)> GradeQuestionAsync(AiGradeRequestDto request, string? userId);
        Task<(bool Success, int StatusCode, string? ErrorMessage, AiGradeResponseDto? Result)> GradeQuestionImageAsync(AiGradeImageRequestDto request, string? userId);
        Task<(bool Success, int StatusCode, string? ErrorMessage, AiBatchGradeResponseDto? Result)> GradeQuizBatchAsync(AiBatchGradeRequestDto request, string? userId);
        Task<(bool Success, int StatusCode, string? ErrorMessage, QuizJobStatusResponseDto? Result)> SubmitQuizAsync(QuizSubmissionRequestDto request, string userId);
        Task<QuizJobStatusResponseDto?> GetQuizJobStatusAsync(string jobId);
    }
}
