using System;
using System.Linq;
using System.Threading.Tasks;
using EdCo.AdminPortal.Models;
using EdCo.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class SubscriptionsController : Controller
    {
        private readonly EdCoDbContext _context;
        private readonly EdCo.Core.Interfaces.IAuditLogService _auditLogService;

        public SubscriptionsController(EdCoDbContext context, EdCo.Core.Interfaces.IAuditLogService auditLogService)
        {
            _context = context;
            _auditLogService = auditLogService;
        }

        private string GetCurrentUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        private string GetCurrentUserName() => User.Identity?.Name ?? "Admin";
        private string GetCurrentUserRole() => User.IsInRole("SuperAdmin") ? "SuperAdmin" : "Admin";
        private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? search = null, string? status = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var now = DateTime.UtcNow;

            // 1. Efficient KPI Calculations via SQL Server Direct Aggregations
            int totalActive = await _context.Users.CountAsync(u => u.IsSubscribed && u.SubscriptionEndDate >= now);
            
            decimal totalRevenue = await _context.Users
                .Where(u => u.IsSubscribed && u.SubscriptionEndDate >= now)
                .SumAsync(u => u.GradeLevel != null ? u.GradeLevel.TierPrice : 0);

            int totalEverSubscribed = await _context.Users.CountAsync(u => u.IsSubscribed);

            double churnRate = totalEverSubscribed > 0
                ? (double)(totalEverSubscribed - totalActive) / totalEverSubscribed
                : 0.0;

            // 2. Server-Side Paginated Query for Subscriber Table
            var query = _context.Users
                .Include(u => u.GradeLevel)
                .Where(u => u.IsSubscribed);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u => (u.FullName != null && u.FullName.Contains(term)) ||
                                         (u.Email != null && u.Email.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(u => u.SubscriptionEndDate >= now);
                }
                else if (status.Equals("expired", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(u => u.SubscriptionEndDate < now);
                }
            }

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages < 1) totalPages = 1;

            var subscribers = await query
                .OrderByDescending(u => u.SubscriptionEndDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new SubscriptionItem
                {
                    Id = s.Id,
                    FullName = s.FullName ?? string.Empty,
                    Email = s.Email ?? string.Empty,
                    GradeLevelName = s.GradeLevel != null ? s.GradeLevel.Name : "Unknown",
                    TierPrice = s.GradeLevel != null ? s.GradeLevel.TierPrice : 0,
                    SubscriptionEndDate = s.SubscriptionEndDate,
                    IsActive = s.SubscriptionEndDate >= now
                })
                .ToListAsync();

            var gradeLevels = await _context.GradeLevels.OrderBy(g => g.Name).ToListAsync();

            var vm = new SubscriptionsViewModel
            {
                TotalActiveSubscribers = totalActive,
                TotalRevenue = totalRevenue,
                ChurnRate = churnRate,
                Subscribers = subscribers,
                GradeLevels = gradeLevels,
                PageIndex = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalItems = totalItems,
                SearchTerm = search,
                StatusFilter = status
            };

            return View(vm);
        }

        // POST: /Subscriptions/UpdateTierRates
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTierRates(int gradeId, decimal tierPrice, int subscriptionDurationDays)
        {
            var grade = await _context.GradeLevels.FindAsync(gradeId);
            if (grade == null) return NotFound();

            grade.TierPrice = tierPrice;
            grade.SubscriptionDurationDays = subscriptionDurationDays > 0 ? subscriptionDurationDays : 90;
            await _context.SaveChangesAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "UpdateSubscriptionRates",
                entityName: "GradeLevel",
                entityId: gradeId.ToString(),
                details: $"Updated tier rates for '{grade.Name}': Fee=${tierPrice:F2}, Duration={grade.SubscriptionDurationDays} days",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            TempData["Success"] = $"Subscription fee (${tierPrice:F2}) and duration ({grade.SubscriptionDurationDays} days) updated for {grade.Name}.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Subscriptions/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string userId, bool isSubscribed, int extensionDays = 30)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();

            user.IsSubscribed = isSubscribed;
            if (isSubscribed)
            {
                user.SubscriptionEndDate = DateTime.UtcNow.AddDays(extensionDays);
            }
            else
            {
                user.SubscriptionEndDate = null;
            }

            await _context.SaveChangesAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "ChangeSubscriptionSettings",
                entityName: "Subscription",
                entityId: userId,
                details: $"Updated subscription for '{user.Email}': IsSubscribed={isSubscribed}, ExtensionDays={extensionDays}",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            TempData["Success"] = $"Subscription status updated for {user.Email}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
