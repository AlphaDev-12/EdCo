using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EdCo.Core.Data;
using EdCo.Core.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using EdCo.AdminPortal.Models;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class DashboardController : Controller
    {
        private readonly EdCoDbContext _context;
        private readonly ICacheService _cacheService;

        public DashboardController(EdCoDbContext context, ICacheService cacheService)
        {
            _context = context;
            _cacheService = cacheService;
        }

        public async Task<IActionResult> Index()
        {
            const string cacheKey = "Dashboard:Metrics";

            var vm = await _cacheService.GetOrCreateAsync(cacheKey, async () =>
            {
                var model = new DashboardViewModel();
                var now = DateTime.UtcNow;

                // KPIs
                model.TotalStudents = await _context.Users.CountAsync();
                model.ActiveSubscriptions = await _context.Users.CountAsync(u => u.IsSubscribed && u.SubscriptionEndDate >= now);
                model.TotalVideos = await _context.VideoAssets.CountAsync();
                model.QuizzesTaken = await _context.QuizResults.CountAsync();

                // Recent Activity (combine new users and new quizzes)
                var recentUsers = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(5)
                    .Select(u => new ActivityItem
                    {
                        Icon = "fa-user-plus",
                        IconColorClass = "text-primary",
                        Text = $"New student registered: {u.FullName ?? u.UserName}",
                        Timestamp = u.CreatedAt
                    }).ToListAsync();

                var recentQuizzes = await _context.QuizResults
                    .Include(qr => qr.Quiz)
                        .ThenInclude(q => q.Unit)
                    .OrderByDescending(qr => qr.AttemptedAt)
                    .Take(5)
                    .Select(qr => new ActivityItem
                    {
                        Icon = "fa-pen-to-square",
                        IconColorClass = "text-info",
                        Text = $"Quiz taken: {(qr.Quiz != null && qr.Quiz.Unit != null ? qr.Quiz.Unit.Title : (qr.Quiz != null ? qr.Quiz.Title : "Quiz"))} (Score: {qr.Score}/{qr.TotalQuestions})",
                        Timestamp = qr.AttemptedAt
                    }).ToListAsync();

                model.RecentActivity = recentUsers.Concat(recentQuizzes)
                    .OrderByDescending(a => a.Timestamp)
                    .Take(7)
                    .ToList();

                // Chart Data: Signups over last 7 days
                var today = now.Date;
                var sevenDaysAgo = today.AddDays(-6);

                var signups = await _context.Users
                    .Where(u => u.CreatedAt >= sevenDaysAgo)
                    .GroupBy(u => u.CreatedAt.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .ToListAsync();

                for (int i = 0; i < 7; i++)
                {
                    var date = sevenDaysAgo.AddDays(i);
                    model.ChartLabels.Add(date.ToString("MMM dd"));
                    var count = signups.FirstOrDefault(s => s.Date.Date == date.Date)?.Count ?? 0;
                    model.ChartData.Add(count);
                }

                return model;
            }, TimeSpan.FromMinutes(5));

            return View(vm);
        }
    }
}
