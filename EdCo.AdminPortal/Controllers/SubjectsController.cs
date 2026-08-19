using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class SubjectsController : Controller
    {
        private readonly EdCoDbContext _context;
        private readonly ICacheService _cacheService;
        private readonly IAuditLogService _auditLogService;

        public SubjectsController(EdCoDbContext context, ICacheService cacheService, IAuditLogService auditLogService)
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
            var subjects = await _cacheService.GetOrCreateAsync("Dropdowns:SubjectsList", async () =>
            {
                return await _context.Subjects
                    .Include(s => s.GradeLevel)
                    .Include(s => s.Chapters)
                    .OrderBy(s => s.GradeLevel.Name)
                    .ThenBy(s => s.Name)
                    .ToListAsync();
            }, TimeSpan.FromMinutes(30));

            ViewBag.GradeLevels = await _cacheService.GetOrCreateAsync("Dropdowns:GradeLevelsSelectList", async () =>
            {
                return await _context.GradeLevels
                    .Where(g => g.IsActive)
                    .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                    .ToListAsync();
            }, TimeSpan.FromMinutes(30));

            return View(subjects);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name, int gradeLevelId)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Subject name is required.";
                return RedirectToAction(nameof(Index));
            }

            var subject = new Subject
            {
                Name = name,
                GradeLevelId = gradeLevelId
            };
            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            await InvalidateCacheAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "CreateSubject",
                entityName: "Subject",
                entityId: subject.Id.ToString(),
                details: $"Created subject '{name}' for grade level #{gradeLevelId}",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            TempData["Success"] = $"Subject '{name}' created successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null) return NotFound();

            subject.DeletedBy = GetCurrentUserName();
            _context.Subjects.Remove(subject);
            await _context.SaveChangesAsync();

            await InvalidateCacheAsync();

            await _auditLogService.LogAdminActionAsync(
                action: "DeleteSubject",
                entityName: "Subject",
                entityId: id.ToString(),
                details: $"Soft deleted subject '{subject.Name}' (Id: {id})",
                userId: GetCurrentUserId(),
                userName: GetCurrentUserName(),
                userRole: GetCurrentUserRole(),
                ipAddress: GetClientIp());

            TempData["Success"] = $"Subject '{subject.Name}' deleted (soft-delete).";
            return RedirectToAction(nameof(Index));
        }

        private async Task InvalidateCacheAsync()
        {
            await _cacheService.RemoveAsync("Dropdowns:SubjectsList");
            await _cacheService.RemoveByPrefixAsync("Curriculum:Subjects:");
            await _cacheService.RemoveByPrefixAsync("Curriculum:Manifest:");
        }
    }
}
