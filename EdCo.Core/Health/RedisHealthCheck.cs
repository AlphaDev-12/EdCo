using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EdCo.Core.Health
{
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly IDistributedCache _cache;

        public RedisHealthCheck(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var healthKey = $"_health_check_{Guid.NewGuid():N}";
                await _cache.SetStringAsync(healthKey, "ok", new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(10)
                }, cancellationToken);

                var value = await _cache.GetStringAsync(healthKey, cancellationToken);
                await _cache.RemoveAsync(healthKey, cancellationToken);

                if (value == "ok")
                {
                    return HealthCheckResult.Healthy("Distributed Cache (Redis/Memory) probe succeeded.");
                }

                return HealthCheckResult.Degraded("Distributed Cache probe returned invalid payload.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Distributed Cache probe failed.", ex);
            }
        }
    }
}
