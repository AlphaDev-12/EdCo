using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EdCo.Core.Entities;
using Microsoft.AspNetCore.Http;

namespace EdCo.Core.Interfaces
{
    public interface IErrorLogService
    {
        Task LogErrorAsync(
            Exception exception,
            string source,
            HttpContext? httpContext = null,
            string logLevel = "Error",
            string? customMessage = null);

        Task<(IEnumerable<AppErrorLog> Items, int TotalCount)> GetErrorLogsAsync(
            int page = 1,
            int pageSize = 20,
            string? source = null,
            string? logLevel = null,
            bool? isResolved = null,
            string? search = null);

        Task<AppErrorLog?> GetErrorByIdAsync(int id);

        Task<bool> ResolveErrorAsync(int id, string resolvedBy, string? resolutionNotes);

        Task<Dictionary<string, int>> GetErrorStatsAsync();
    }
}
