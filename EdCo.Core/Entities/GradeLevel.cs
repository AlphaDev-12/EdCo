using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace EdCo.Core.Entities
{
    public class GradeLevel : ISoftDelete
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TierPrice { get; set; }
        public bool IsActive { get; set; } = true;
        public int SubscriptionDurationDays { get; set; } = 90;
        
        public ICollection<Subject> Subjects { get; set; } = new List<Subject>();

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }
}

