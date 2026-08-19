using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class GradeLevelsController : Controller
    {
        private readonly EdCoDbContext _context;
        private readonly ICacheService _cacheService;
        private readonly IAuditLogService _auditLogService;
        private const string GradeLevelsCacheKey = "Dropdowns:GradeLevels";

        public GradeLevelsController(EdCoDbContext context, ICacheService cacheService, IAuditLogService auditLogService)
        {
            _context = context;
            _cacheService = cacheService;
            _auditLogService = auditLogService;
        }

        private string GetCurrentUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        private string GetCurrentUserName() => User.Identity?.Name ?? "Admin";
        private string GetCurrentUserRole() => User.IsInRole("SuperAdmin") ? "SuperAdmin" : "Admin";
        private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        public async Task<IActionResult> Index()
        {
            var grades = await _cacheService.GetOrCreateAsync(GradeLevelsCacheKey, async () =>
            {
                return await _context.GradeLevels
                    .Include(g => g.Subjects)
                    .OrderBy(g => g.Name)
                    .ToListAsync();
            }, TimeSpan.FromMinutes(30));

            return View(grades);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, decimal tierPrice, int subscriptionDurationDays = 90)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Grade name is required.";
                return RedirectToAction(nameof(Index));
            }

            var grade = new GradeLevel
            {
                Name = name,
                TierPrice = tierPrice,
                SubscriptionDurationDays = subscriptionDurationDays > 0 ? subscriptionDurationDays : 90,
                IsActive = true
            };
            _context.GradeLevels.Add(grade);
            await _context.SaveChangesAsync();

            await InvalidateCacheAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "CreateGradeLevel",
                entityName: "GradeLevel",
                entityId: grade.Id.ToString(),
                details: $"Created grade level '{name}' with tier price {tierPrice:C} and duration {grade.SubscriptionDurationDays} days",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            TempData["Success"] = $"Grade Level '{name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string name, decimal tierPrice, int subscriptionDurationDays, bool isActive)
        {
            var grade = await _context.GradeLevels.FindAsync(id);
            if (grade == null) return NotFound();

            grade.Name = name;
            grade.TierPrice = tierPrice;
            grade.SubscriptionDurationDays = subscriptionDurationDays > 0 ? subscriptionDurationDays : 90;
            grade.IsActive = isActive;
            await _context.SaveChangesAsync();

            await InvalidateCacheAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "UpdateGradeLevel",
                entityName: "GradeLevel",
                entityId: grade.Id.ToString(),
                details: $"Updated grade level '{name}' (Price: {tierPrice:C}, Duration: {grade.SubscriptionDurationDays} days, Active: {isActive})",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            TempData["Success"] = $"Grade Level '{name}' updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var grade = await _context.GradeLevels.FindAsync(id);
            if (grade == null) return NotFound();

            grade.DeletedBy = GetCurrentUserName();
            _context.GradeLevels.Remove(grade);
            await _context.SaveChangesAsync();

            await InvalidateCacheAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "DeleteGradeLevel",
                entityName: "GradeLevel",
                entityId: id.ToString(),
                details: $"Soft deleted grade level '{grade.Name}' (Id: {id})",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            TempData["Success"] = $"Grade Level '{grade.Name}' deleted (soft-delete).";
            return RedirectToAction(nameof(Index));
        }

        private async Task InvalidateCacheAsync()
        {
            await _cacheService.RemoveAsync(GradeLevelsCacheKey);
            await _cacheService.RemoveByPrefixAsync("Curriculum:Subjects:");
        }
    }
}
