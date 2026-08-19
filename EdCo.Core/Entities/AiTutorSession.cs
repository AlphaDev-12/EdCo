using System;
using System.Collections.Generic;

namespace EdCo.Core.Entities
{
    public class AiTutorSession : ISoftDelete
    {
        public Guid Id { get; set; }
        public string? AppUserId { get; set; }
        public AppUser? AppUser { get; set; }
        
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;
        
        public string Topic { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastInteractionAt { get; set; } = DateTime.UtcNow;
        
        public ICollection<AiTutorInteraction> Interactions { get; set; } = new List<AiTutorInteraction>();

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}
