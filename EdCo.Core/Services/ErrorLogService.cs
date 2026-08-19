using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EdCo.Core.Services
{
    public class ErrorLogService : IErrorLogService
    {
        private readonly EdCoDbContext _context;
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public ErrorLogService(EdCoDbContext context, IHttpContextAccessor? httpContextAccessor = null)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogErrorAsync(
            Exception exception,
            string source,
            HttpContext? httpContext = null,
            string logLevel = "Error",
            string? customMessage = null)
        {
            try
            {
                var errorLog = new AppErrorLog
                {
                    CreatedAt = DateTime.UtcNow,
                    LogLevel = string.IsNullOrWhiteSpace(logLevel) ? "Error" : logLevel,
                    Source = string.IsNullOrWhiteSpace(source) ? "System" : source,
                    Message = customMessage ?? exception.Message,
                    ExceptionType = exception.GetType().FullName ?? exception.GetType().Name,
                    StackTrace = exception.ToString(),
                    IsResolved = false
                };

                var effectiveContext = httpContext ?? _httpContextAccessor?.HttpContext;

                if (effectiveContext != null)
                {
                    errorLog.RequestPath = effectiveContext.Request.Path;
                    errorLog.HttpMethod = effectiveContext.Request.Method;
                    errorLog.StatusCode = effectiveContext.Response?.StatusCode;
                    errorLog.TraceId = effectiveContext.TraceIdentifier;

                    if (effectiveContext.User?.Identity?.IsAuthenticated == true)
                    {
                        errorLog.UserId = effectiveContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        errorLog.UserName = effectiveContext.User.Identity.Name;
                    }
                }

                _context.ErrorLogs.Add(errorLog);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Fallback console log to avoid recursive failures
                Console.WriteLine($"[ErrorLogService] Failed to record error log to database: {ex.Message}");
            }
        }

        public async Task<(IEnumerable<AppErrorLog> Items, int TotalCount)> GetErrorLogsAsync(
            int page = 1,
            int pageSize = 20,
            string? source = null,
            string? logLevel = null,
            bool? isResolved = null,
            string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _context.ErrorLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(source) && source != "All")
            {
                query = query.Where(e => e.Source == source);
            }

            if (!string.IsNullOrWhiteSpace(logLevel) && logLevel != "All")
            {
                query = query.Where(e => e.LogLevel == logLevel);
            }

            if (isResolved.HasValue)
            {
                query = query.Where(e => e.IsResolved == isResolved.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                query = query.Where(e =>
                    e.Message.Contains(s) ||
                    (e.RequestPath != null && e.RequestPath.Contains(s)) ||
                    (e.TraceId != null && e.TraceId.Contains(s)) ||
                    (e.ExceptionType != null && e.ExceptionType.Contains(s)) ||
                    (e.UserName != null && e.UserName.Contains(s)));
            }

            int totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<AppErrorLog?> GetErrorByIdAsync(int id)
        {
            return await _context.ErrorLogs.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
        }

        public async Task<bool> ResolveErrorAsync(int id, string resolvedBy, string? resolutionNotes)
        {
            var error = await _context.ErrorLogs.FirstOrDefaultAsync(e => e.Id == id);
            if (error == null) return false;

            error.IsResolved = true;
            error.ResolvedBy = resolvedBy;
            error.ResolvedAt = DateTime.UtcNow;
            error.ResolutionNotes = resolutionNotes;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Dictionary<string, int>> GetErrorStatsAsync()
        {
            var now = DateTime.UtcNow;
            var past24h = now.AddHours(-24);

            int totalUnresolved = await _context.ErrorLogs.CountAsync(e => !e.IsResolved);
            int errors24h = await _context.ErrorLogs.CountAsync(e => e.CreatedAt >= past24h);
            int apiErrors = await _context.ErrorLogs.CountAsync(e => e.Source == "API" && !e.IsResolved);
            int adminErrors = await _context.ErrorLogs.CountAsync(e => e.Source == "AdminPortal" && !e.IsResolved);

            return new Dictionary<string, int>
            {
                { "TotalUnresolved", totalUnresolved },
                { "Errors24h", errors24h },
                { "ApiErrors", apiErrors },
                { "AdminErrors", adminErrors }
            };
        }
    }
}
