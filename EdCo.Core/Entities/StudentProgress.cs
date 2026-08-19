using System;

namespace EdCo.Core.Entities
{
    public class StudentProgress : ISoftDelete
    {
        public int Id { get; set; }
        
        public string AppUserId { get; set; } = string.Empty;
        public AppUser AppUser { get; set; } = null!;
        
        public int UnitId { get; set; }
        public Unit Unit { get; set; } = null!;
        
        public bool IsVideoWatched { get; set; }
        public bool IsNotesRead { get; set; }
        public bool IsQuizPassed { get; set; }
        
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}
