using System;
using System.Collections.Generic;

namespace EdCo.AdminPortal.Models
{
    public class DashboardViewModel
    {
        public int TotalStudents { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int TotalVideos { get; set; }
        public int QuizzesTaken { get; set; }

        public List<ActivityItem> RecentActivity { get; set; } = new List<ActivityItem>();

        public List<string> ChartLabels { get; set; } = new List<string>();
        public List<int> ChartData { get; set; } = new List<int>();
    }

    public class ActivityItem
    {
        public string Icon { get; set; } = string.Empty;
        public string IconColorClass { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
