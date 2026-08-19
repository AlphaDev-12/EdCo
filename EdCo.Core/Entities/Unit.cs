using System;

namespace EdCo.Core.Entities
{
    public class Unit : ISoftDelete
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        
        public int ChapterId { get; set; }
        public Chapter Chapter { get; set; } = null!;
        
        public VideoAsset? Video { get; set; }
        public NotesContent? Notes { get; set; }
        public Quiz? Quiz { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}

