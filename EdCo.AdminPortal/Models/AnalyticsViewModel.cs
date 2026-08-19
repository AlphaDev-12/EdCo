using System;
using System.Collections.Generic;

namespace EdCo.AdminPortal.Models
{
    public class AnalyticsViewModel
    {
        public double QuizPassRate { get; set; }
        public int TotalOfflineSyncs { get; set; }
        public int AiConversations { get; set; }
        public int TotalTokensUsed { get; set; }
        public int TotalInputTokens { get; set; }
        public int TotalOutputTokens { get; set; }
        public decimal TotalCost { get; set; }

        // Chart 1: Daily Student Engagement (e.g. Quiz Attempts)
        public List<string> EngagementChartLabels { get; set; } = new List<string>();
        public List<int> EngagementChartData { get; set; } = new List<int>();

        // Chart 2: Token Usage Over Time
        public List<string> TokenChartLabels { get; set; } = new List<string>();
        public List<int> TokenChartData { get; set; } = new List<int>();
    }
}
