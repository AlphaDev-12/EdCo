using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AuditLogsController : Controller
    {
        private readonly IAuditLogService _auditLogService;
        private readonly EdCoDbContext _context;

        public AuditLogsController(IAuditLogService auditLogService, EdCoDbContext context)
        {
            _auditLogService = auditLogService;
            _context = context;
        }

        private string GetCurrentUserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        private string GetCurrentUserName() => User.Identity?.Name ?? "Admin";
        private string GetCurrentUserRole() => User.IsInRole("SuperAdmin") ? "SuperAdmin" : "Admin";
        private string GetClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        // GET: /AuditLogs - Admin Audit Trail
        public async Task<IActionResult> Index(int page = 1, string? search = null, string? actionFilter = null, string? entityFilter = null)
        {
            const int pageSize = 20;
            var (items, totalCount) = await _auditLogService.GetAuditLogsAsync(page, pageSize, search, actionFilter, entityFilter);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.Search = search;
            ViewBag.ActionFilter = actionFilter;
            ViewBag.EntityFilter = entityFilter;

            return View(items);
        }

        // GET: /AuditLogs/StudentActivity - Student Usage Telemetry Dashboard
        public async Task<IActionResult> StudentActivity(int page = 1, string? search = null, string? activityType = null)
        {
            const int pageSize = 20;
            var summary = await _auditLogService.GetStudentUsageSummaryAsync();
            var (items, totalCount) = await _auditLogService.GetStudentActivitiesAsync(page, pageSize, search, activityType);

            ViewBag.Summary = summary;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.Search = search;
            ViewBag.ActivityType = activityType;

            return View(items);
        }

        // GET: /AuditLogs/SoftDeleted - SuperAdmin Soft-Delete Recovery Center
        public async Task<IActionResult> SoftDeleted(string entityType = "Chapter")
        {
            ViewBag.EntityType = entityType;

            if (entityType.Equals("Unit", StringComparison.OrdinalIgnoreCase))
            {
                var units = await _context.Units
                    .IgnoreQueryFilters()
                    .Where(u => u.IsDeleted)
                    .OrderByDescending(u => u.DeletedAt)
                    .ToListAsync();
                return View(units);
            }
            else if (entityType.Equals("Quiz", StringComparison.OrdinalIgnoreCase))
            {
                var quizzes = await _context.Quizzes
                    .IgnoreQueryFilters()
                    .Where(q => q.IsDeleted)
                    .OrderByDescending(q => q.DeletedAt)
                    .ToListAsync();
                return View(quizzes);
            }
            else if (entityType.Equals("Subject", StringComparison.OrdinalIgnoreCase))
            {
                var subjects = await _context.Subjects
                    .IgnoreQueryFilters()
                    .Where(s => s.IsDeleted)
                    .OrderByDescending(s => s.DeletedAt)
                    .ToListAsync();
                return View(subjects);
            }
            else if (entityType.Equals("GradeLevel", StringComparison.OrdinalIgnoreCase))
            {
                var grades = await _context.GradeLevels
                    .IgnoreQueryFilters()
                    .Where(g => g.IsDeleted)
                    .OrderByDescending(g => g.DeletedAt)
                    .ToListAsync();
                return View(grades);
            }
            else
            {
                var chapters = await _context.Chapters
                    .IgnoreQueryFilters()
                    .Where(c => c.IsDeleted)
                    .OrderByDescending(c => c.DeletedAt)
                    .ToListAsync();
                return View(chapters);
            }
        }

        // POST: /AuditLogs/RestoreEntity - Restore a soft-deleted entity
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> RestoreEntity(string entityType, int id)
        {
            bool restored = false;
            string entityName = "";

            if (entityType.Equals("Chapter", StringComparison.OrdinalIgnoreCase))
            {
                var item = await _context.Chapters.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == id && c.IsDeleted);
                if (item != null)
                {
                    item.IsDeleted = false;
                    item.DeletedAt = null;
                    item.DeletedBy = null;
                    entityName = item.Title;
                    restored = true;
                }
            }
            else if (entityType.Equals("Unit", StringComparison.OrdinalIgnoreCase))
            {
                var item = await _context.Units.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id && u.IsDeleted);
                if (item != null)
                {
                    item.IsDeleted = false;
                    item.DeletedAt = null;
                    item.DeletedBy = null;
                    entityName = item.Title;
                    restored = true;
                }
            }
            else if (entityType.Equals("Quiz", StringComparison.OrdinalIgnoreCase))
            {
                var item = await _context.Quizzes.IgnoreQueryFilters().FirstOrDefaultAsync(q => q.Id == id && q.IsDeleted);
                if (item != null)
                {
                    item.IsDeleted = false;
                    item.DeletedAt = null;
                    item.DeletedBy = null;
                    entityName = item.Title;
                    restored = true;
                }
            }
            else if (entityType.Equals("Subject", StringComparison.OrdinalIgnoreCase))
            {
                var item = await _context.Subjects.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id && s.IsDeleted);
                if (item != null)
                {
                    item.IsDeleted = false;
                    item.DeletedAt = null;
                    item.DeletedBy = null;
                    entityName = item.Name;
                    restored = true;
                }
            }
            else if (entityType.Equals("GradeLevel", StringComparison.OrdinalIgnoreCase))
            {
                var item = await _context.GradeLevels.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.Id == id && g.IsDeleted);
                if (item != null)
                {
                    item.IsDeleted = false;
                    item.DeletedAt = null;
                    item.DeletedBy = null;
                    entityName = item.Name;
                    restored = true;
                }
            }

            if (restored)
            {
                await _context.SaveChangesAsync();

                await _auditLogService.LogAdminActionAsync(
                    action: "RestoreSoftDeletedEntity",
                    entityName: entityType,
                    entityId: id.ToString(),
                    details: $"Restored soft-deleted {entityType} '{entityName}' (Id: {id})",
                    userId: GetCurrentUserId(),
                    userName: GetCurrentUserName(),
                    userRole: GetCurrentUserRole(),
                    ipAddress: GetClientIp());

                TempData["Success"] = $"Successfully restored {entityType} '{entityName}'.";
            }
            else
            {
                TempData["Error"] = $"Entity {entityType} #{id} not found or not soft-deleted.";
            }

            return RedirectToAction(nameof(SoftDeleted), new { entityType });
        }
    }
}
