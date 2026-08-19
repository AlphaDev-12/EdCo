using System;

namespace EdCo.Core.Entities
{
    public class StudentFlashcardProgress : ISoftDelete
    {
        public int Id { get; set; }
        
        public string AppUserId { get; set; } = string.Empty;
        public AppUser AppUser { get; set; } = null!;
        
        public int FlashcardId { get; set; }
        public Flashcard Flashcard { get; set; } = null!;
        
        public bool IsMastered { get; set; }
        public DateTime MasteredAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}
