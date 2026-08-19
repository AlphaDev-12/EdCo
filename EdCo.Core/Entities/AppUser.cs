using Microsoft.AspNetCore.Identity;

namespace EdCo.Core.Entities
{
    public class AppUser : IdentityUser
    {
        public string? FullName { get; set; }
        
        // Navigation property for filtering subjects
        public int? GradeLevelId { get; set; }
        public GradeLevel? GradeLevel { get; set; }

        // Subscription properties
        public bool IsSubscribed { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
