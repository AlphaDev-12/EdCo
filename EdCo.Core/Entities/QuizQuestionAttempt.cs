using System;

namespace EdCo.Core.Entities
{
    public class QuizQuestionAttempt : ISoftDelete
    {
        public int Id { get; set; }
        
        public string AppUserId { get; set; } = string.Empty;
        public AppUser AppUser { get; set; } = null!;
        
        public int QuizQuestionId { get; set; }
        public QuizQuestion QuizQuestion { get; set; } = null!;
        
        public bool IsCorrect { get; set; }
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}
