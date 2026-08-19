using System;

namespace EdCo.Core.Entities
{
    public class Chapter : ISoftDelete
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        
        public int SubjectId { get; set; }
        public Subject Subject { get; set; } = null!;
        
        public ICollection<Unit> Units { get; set; } = new List<Unit>();

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}

