using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EdCo.Core.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AnalyticsController : Controller
    {
        private readonly EdCoDbContext _context;

        public AnalyticsController(EdCoDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new EdCo.AdminPortal.Models.AnalyticsViewModel();

            // KPIs
            var totalQuizzes = await _context.QuizResults.CountAsync();
            if (totalQuizzes > 0)
            {
                var passedQuizzes = await _context.QuizResults.CountAsync(qr => ((double)qr.Score / qr.TotalQuestions) >= 0.5);
                vm.QuizPassRate = Math.Round(((double)passedQuizzes / totalQuizzes) * 100, 1);
            }
            else
            {
                vm.QuizPassRate = 0;
            }

            vm.TotalOfflineSyncs = await _context.QuizResults.CountAsync(qr => qr.IsSyncedOnline);
            vm.AiConversations = await _context.AiInteractionLogs.CountAsync();
            vm.TotalTokensUsed = await _context.AiInteractionLogs.SumAsync(a => a.TotalTokens);
            vm.TotalInputTokens = await _context.AiInteractionLogs.SumAsync(a => a.PromptTokens);
            vm.TotalOutputTokens = await _context.AiInteractionLogs.SumAsync(a => a.CompletionTokens);
            vm.TotalCost = await _context.AiInteractionLogs.SumAsync(a => a.Cost);

            var today = DateTime.UtcNow.Date;
            var sevenDaysAgo = today.AddDays(-6);

            // Chart 1: Daily Engagement (Quiz Attempts)
            var engagementData = await _context.QuizResults
                .Where(qr => qr.AttemptedAt >= sevenDaysAgo)
                .GroupBy(qr => qr.AttemptedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            // Chart 2: Token Usage Over Time
            var tokenData = await _context.AiInteractionLogs
                .Where(a => a.Timestamp >= sevenDaysAgo)
                .GroupBy(a => a.Timestamp.Date)
                .Select(g => new { Date = g.Key, Tokens = g.Sum(x => x.TotalTokens) })
                .ToListAsync();

            for (int i = 0; i < 7; i++)
            {
                var date = sevenDaysAgo.AddDays(i);
                var label = date.ToString("MMM dd");
                
                vm.EngagementChartLabels.Add(label);
                vm.EngagementChartData.Add(engagementData.FirstOrDefault(d => d.Date.Date == date.Date)?.Count ?? 0);

                vm.TokenChartLabels.Add(label);
                vm.TokenChartData.Add(tokenData.FirstOrDefault(d => d.Date.Date == date.Date)?.Tokens ?? 0);
            }

            return View(vm);
        }
    }
}
