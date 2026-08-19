using System;
using System.Collections.Generic;

namespace EdCo.AdminPortal.Models
{
    public class SubscriptionsViewModel
    {
        public int TotalActiveSubscribers { get; set; }
        public decimal TotalRevenue { get; set; }
        public double ChurnRate { get; set; }
        
        public List<SubscriptionItem> Subscribers { get; set; } = new List<SubscriptionItem>();
        public List<EdCo.Core.Entities.GradeLevel> GradeLevels { get; set; } = new List<EdCo.Core.Entities.GradeLevel>();

        // Pagination & Filter metadata
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; } = 1;
        public int TotalItems { get; set; } = 0;
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage => PageIndex < TotalPages;
    }

    public class SubscriptionItem
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string GradeLevelName { get; set; } = string.Empty;
        public decimal TierPrice { get; set; }
        public DateTime? SubscriptionEndDate { get; set; }
        public bool IsActive { get; set; }
    }
}
