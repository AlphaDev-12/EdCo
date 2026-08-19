using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EdCo.Core.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EdCo.Core.Services
{
    public class RedisCacheService : ICacheService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<RedisCacheService> _logger;
        private readonly ConcurrentDictionary<string, bool> _trackedKeys = new ConcurrentDictionary<string, bool>();
        private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(10);

        public RedisCacheService(
            IDistributedCache distributedCache,
            IMemoryCache memoryCache,
            ILogger<RedisCacheService> logger)
        {
            _distributedCache = distributedCache;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            try
            {
                var bytes = await _distributedCache.GetAsync(key);
                if (bytes != null && bytes.Length > 0)
                {
                    var json = System.Text.Encoding.UTF8.GetString(bytes);
                    return JsonSerializer.Deserialize<T>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis GetAsync failed for key '{Key}'. Falling back to local memory cache.", key);
            }

            // Fallback to local memory cache
            if (_memoryCache.TryGetValue(key, out T? localValue))
            {
                return localValue;
            }

            return default;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            var exp = expiration ?? _defaultExpiration;
            _trackedKeys[key] = true;

            // Also keep local memory cache warm as L1 fallback
            _memoryCache.Set(key, value, exp);

            try
            {
                var json = JsonSerializer.Serialize(value);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                var options = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = exp
                };
                await _distributedCache.SetAsync(key, bytes, options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis SetAsync failed for key '{Key}'. Relying on local memory cache.", key);
            }
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            var existing = await GetAsync<T>(key);
            if (existing != null)
            {
                return existing;
            }

            var newValue = await factory();
            if (newValue != null)
            {
                await SetAsync(key, newValue, expiration);
            }
            return newValue;
        }

        public async Task RemoveAsync(string key)
        {
            _trackedKeys.TryRemove(key, out _);
            _memoryCache.Remove(key);

            try
            {
                await _distributedCache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis RemoveAsync failed for key '{Key}'. Removed from local memory cache.", key);
            }
        }

        public async Task RemoveByPrefixAsync(string prefix)
        {
            var matchingKeys = _trackedKeys.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var key in matchingKeys)
            {
                await RemoveAsync(key);
            }
        }
    }
}
