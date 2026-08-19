using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using EdCo.Core.Data;

namespace EdCo.Core.Services
{
    public class AuditLogCleanupHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AuditLogCleanupHostedService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);
        private readonly int _retentionDays = 90;

        public AuditLogCleanupHostedService(
            IServiceProvider serviceProvider,
            ILogger<AuditLogCleanupHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AuditLogCleanupHostedService started. Retention period set to {Days} days.", _retentionDays);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while pruning old audit and telemetry logs.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task PerformCleanupAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EdCoDbContext>();

            var cutoffDate = DateTime.UtcNow.AddDays(-_retentionDays);

            // Execute batch deletion for expired audit logs
            int deletedAuditLogs = await dbContext.AuditLogs
                .Where(a => a.Timestamp < cutoffDate)
                .ExecuteDeleteAsync();

            int deletedAiLogs = await dbContext.AiInteractionLogs
                .Where(a => a.Timestamp < cutoffDate)
                .ExecuteDeleteAsync();

            _logger.LogInformation("Audit log cleanup completed. Pruned {AuditLogsCount} audit logs and {AiLogsCount} AI interaction logs older than {CutoffDate}.", 
                deletedAuditLogs, deletedAiLogs, cutoffDate);
        }
    }
}
