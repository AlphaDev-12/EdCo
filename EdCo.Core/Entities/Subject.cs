using System;

namespace EdCo.Core.Entities
{
    public class Subject : ISoftDelete
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public SubjectType SubjectType { get; set; } = SubjectType.Humanities;
        
        public int GradeLevelId { get; set; }
        public GradeLevel GradeLevel { get; set; } = null!;
        
        public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}

