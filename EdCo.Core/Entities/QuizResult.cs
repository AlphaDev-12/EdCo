using System;

namespace EdCo.Core.Entities
{
    public class QuizResult : ISoftDelete
    {
        public int Id { get; set; }
        
        public string AppUserId { get; set; } = string.Empty;
        public AppUser AppUser { get; set; } = null!;
        
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; } = null!;
        
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
        public bool IsSyncedOnline { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}
