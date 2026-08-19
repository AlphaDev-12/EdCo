using System;
using System.Threading.Tasks;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EdCo.AdminPortal.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class ErrorLogsController : Controller
    {
        private readonly IErrorLogService _errorLogService;

        public ErrorLogsController(IErrorLogService errorLogService)
        {
            _errorLogService = errorLogService;
        }

        // GET: /ErrorLogs
        public async Task<IActionResult> Index(
            int page = 1,
            string? source = null,
            string? logLevel = null,
            bool? isResolved = null,
            string? search = null)
        {
            const int pageSize = 20;

            var (items, totalCount) = await _errorLogService.GetErrorLogsAsync(
                page: page,
                pageSize: pageSize,
                source: source,
                logLevel: logLevel,
                isResolved: isResolved,
                search: search);

            var stats = await _errorLogService.GetErrorStatsAsync();

            ViewBag.Stats = stats;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewBag.TotalCount = totalCount;
            ViewBag.SourceFilter = source;
            ViewBag.LogLevelFilter = logLevel;
            ViewBag.IsResolvedFilter = isResolved;
            ViewBag.Search = search;

            return View(items);
        }

        // GET: /ErrorLogs/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var error = await _errorLogService.GetErrorByIdAsync(id);
            if (error == null)
            {
                return NotFound();
            }

            return Json(error);
        }

        // POST: /ErrorLogs/Resolve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id, string? notes)
        {
            var adminUser = User.Identity?.Name ?? "Admin";
            bool success = await _errorLogService.ResolveErrorAsync(id, adminUser, notes);

            if (success)
            {
                TempData["Success"] = $"Error #{id} successfully marked as resolved.";
            }
            else
            {
                TempData["Error"] = $"Failed to resolve error #{id}. Log record not found.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
