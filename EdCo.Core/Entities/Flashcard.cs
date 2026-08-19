using System;

namespace EdCo.Core.Entities
{
    public class Flashcard : ISoftDelete
    {
        public int Id { get; set; }
        
        public int UnitId { get; set; }
        public Unit Unit { get; set; } = null!;
        
        public string FrontContent { get; set; } = string.Empty;
        public string BackContent { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}

