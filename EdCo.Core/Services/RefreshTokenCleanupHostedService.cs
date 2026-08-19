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
    public class RefreshTokenCleanupHostedService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RefreshTokenCleanupHostedService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24);

        public RefreshTokenCleanupHostedService(
            IServiceProvider serviceProvider,
            ILogger<RefreshTokenCleanupHostedService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RefreshTokenCleanupHostedService started. Cleanup interval set to 24 hours.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformCleanupAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while pruning expired/revoked refresh tokens.");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        public async Task<int> PerformCleanupAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EdCoDbContext>();

            var now = DateTime.UtcNow;

            int deletedCount;
            if (dbContext.Database.IsRelational())
            {
                deletedCount = await dbContext.RefreshTokens
                    .Where(rt => rt.ExpiresAt <= now || rt.RevokedAt != null)
                    .ExecuteDeleteAsync();
            }
            else
            {
                var expiredOrRevoked = await dbContext.RefreshTokens
                    .Where(rt => rt.ExpiresAt <= now || rt.RevokedAt != null)
                    .ToListAsync();
                dbContext.RefreshTokens.RemoveRange(expiredOrRevoked);
                deletedCount = await dbContext.SaveChangesAsync();
            }

            _logger.LogInformation("Refresh token cleanup completed. Pruned {DeletedCount} expired or revoked tokens.", deletedCount);
            return deletedCount;
        }
    }
}
