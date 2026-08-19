using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EdCo.API.DTOs;

namespace EdCo.API.Services
{
    public class GradingQuestionTask
    {
        public int QuestionId { get; set; }
        public string? StudentAnswer { get; set; }
        public string? Base64Image { get; set; }
        public List<string> Base64Images { get; set; } = new();
        public bool IsVision { get; set; }
    }

    public class GradingJobItem
    {
        public string JobId { get; set; } = Guid.NewGuid().ToString();
        public string StudentUserId { get; set; } = string.Empty;
        public int QuizId { get; set; }
        public List<GradingQuestionTask> Questions { get; set; } = new();
        public string Status { get; set; } = "Enqueued"; // Enqueued, Processing, Completed, Failed
        public int TotalQuestions { get; set; }
        public int CompletedQuestions { get; set; }
        public List<AiGradeResponseDtoWithQuestion> Results { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
    }

    public class AiGradeResponseDtoWithQuestion : AiGradeResponseDto
    {
        public int QuestionId { get; set; }
    }

    public interface IGradingJobQueue
    {
        ValueTask EnqueueJobAsync(GradingJobItem job);
        ValueTask<GradingJobItem?> DequeueJobAsync(CancellationToken cancellationToken);
        Task SaveJobStatusAsync(GradingJobItem job);
        Task<GradingJobItem?> GetJobStatusAsync(string jobId);
    }
}
