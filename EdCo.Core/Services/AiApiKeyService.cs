using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdCo.Core.Services
{
    public class AiApiKeyService : IAiApiKeyService
    {
        private readonly EdCoDbContext _db;
        private readonly IAiApiKeyEncryptionService _encryptionService;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAiProviderStrategyFactory _strategyFactory;
        private readonly ILogger<AiApiKeyService> _logger;

        public AiApiKeyService(
            EdCoDbContext db,
            IAiApiKeyEncryptionService encryptionService,
            IMemoryCache cache,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory,
            IAiProviderStrategyFactory strategyFactory,
            ILogger<AiApiKeyService> logger)
        {
            _db = db;
            _encryptionService = encryptionService;
            _cache = cache;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
            _strategyFactory = strategyFactory;
            _logger = logger;
        }

        public async Task<string> GetActiveProviderAsync()
        {
            var cacheKey = "AiSettings_ActiveProvider";
            if (_cache.TryGetValue(cacheKey, out string? cachedProvider) && !string.IsNullOrEmpty(cachedProvider))
            {
                return cachedProvider;
            }

            var dbSetting = await _db.AiApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Provider == "__SYSTEM_SETTING__" && k.Label == "ActiveProvider");

            string activeProvider = dbSetting?.EncryptedApiKey ?? _configuration["AiSettings:ActiveProvider"] ?? "DeepInfra";
            _cache.Set(cacheKey, activeProvider, TimeSpan.FromSeconds(5));
            return activeProvider;
        }

        public async Task<bool> SetActiveProviderAsync(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider)) return false;
            provider = provider.Trim();

            var dbSetting = await _db.AiApiKeys
                .FirstOrDefaultAsync(k => k.Provider == "__SYSTEM_SETTING__" && k.Label == "ActiveProvider");

            if (dbSetting == null)
            {
                dbSetting = new AiApiKey
                {
                    Provider = "__SYSTEM_SETTING__",
                    Label = "ActiveProvider",
                    EncryptedApiKey = provider,
                    MaskedKey = provider,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.AiApiKeys.Add(dbSetting);
            }
            else
            {
                dbSetting.EncryptedApiKey = provider;
                dbSetting.MaskedKey = provider;
            }

            await _db.SaveChangesAsync();

            _configuration["AiSettings:ActiveProvider"] = provider;
            _cache.Remove("AiSettings_ActiveProvider");
            _cache.Set("AiSettings_ActiveProvider", provider, TimeSpan.FromSeconds(5));
            return true;
        }

        public async Task<string> GetActiveKeyAsync(string? provider = null)
        {
            if (string.IsNullOrEmpty(provider))
            {
                provider = await GetActiveProviderAsync();
            }

            var cacheKey = $"AiApiKey_Active_{provider}";

            if (_cache.TryGetValue(cacheKey, out string? cachedKey) && !string.IsNullOrEmpty(cachedKey))
            {
                return cachedKey;
            }

            var activeDbKey = await _db.AiApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.Provider == provider && k.IsActive);

            string resolvedKey;

            if (activeDbKey != null && !string.IsNullOrEmpty(activeDbKey.EncryptedApiKey))
            {
                try
                {
                    resolvedKey = _encryptionService.Decrypt(activeDbKey.EncryptedApiKey);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt active API key ID {KeyId} for provider {Provider}", activeDbKey.Id, provider);
                    resolvedKey = _configuration[$"{provider}:ApiKey"] ?? string.Empty;
                }
            }
            else
            {
                // Fallback to appsettings.json configuration
                resolvedKey = _configuration[$"{provider}:ApiKey"] ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(resolvedKey))
            {
                _cache.Set(cacheKey, resolvedKey, TimeSpan.FromMinutes(15));
            }

            return resolvedKey;
        }

        public async Task<List<AiApiKey>> GetAllKeysAsync(string provider = "Groq")
        {
            return await _db.AiApiKeys
                .AsNoTracking()
                .Where(k => k.Provider == provider)
                .OrderByDescending(k => k.IsActive)
                .ThenByDescending(k => k.CreatedAt)
                .ToListAsync();
        }

        public async Task<AiApiKey> AddKeyAsync(string label, string rawKey, bool setAsActive = true, string provider = "Groq", string? createdBy = null)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
                throw new ArgumentException("API key cannot be empty.", nameof(rawKey));

            rawKey = rawKey.Trim();

            // If setAsActive, deactivate existing keys for this provider
            if (setAsActive)
            {
                var existingActiveKeys = await _db.AiApiKeys
                    .Where(k => k.Provider == provider && k.IsActive)
                    .ToListAsync();

                foreach (var k in existingActiveKeys)
                {
                    k.IsActive = false;
                }
            }

            var masked = MaskKey(rawKey);
            var encrypted = _encryptionService.Encrypt(rawKey);

            var keyEntity = new AiApiKey
            {
                Provider = provider,
                Label = string.IsNullOrWhiteSpace(label) ? $"{provider} Key ({masked})" : label.Trim(),
                EncryptedApiKey = encrypted,
                MaskedKey = masked,
                IsActive = setAsActive,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            };

            _db.AiApiKeys.Add(keyEntity);
            await _db.SaveChangesAsync();

            InvalidateCache(provider);

            return keyEntity;
        }

        public async Task<bool> SetActiveKeyAsync(int keyId, string provider = "Groq")
        {
            var targetKey = await _db.AiApiKeys.FirstOrDefaultAsync(k => k.Id == keyId && k.Provider == provider);
            if (targetKey == null)
                return false;

            var allKeys = await _db.AiApiKeys.Where(k => k.Provider == provider).ToListAsync();
            foreach (var k in allKeys)
            {
                k.IsActive = (k.Id == keyId);
            }

            await _db.SaveChangesAsync();
            InvalidateCache(provider);

            return true;
        }

        public async Task<bool> DeleteKeyAsync(int keyId, string? deletedBy = null)
        {
            var targetKey = await _db.AiApiKeys.FirstOrDefaultAsync(k => k.Id == keyId);
            if (targetKey == null)
                return false;

            string provider = targetKey.Provider;
            bool wasActive = targetKey.IsActive;

            _db.AiApiKeys.Remove(targetKey);
            await _db.SaveChangesAsync();

            // If the deleted key was active, activate the most recently created key if available
            if (wasActive)
            {
                var nextKey = await _db.AiApiKeys
                    .Where(k => k.Provider == provider)
                    .OrderByDescending(k => k.CreatedAt)
                    .FirstOrDefaultAsync();

                if (nextKey != null)
                {
                    nextKey.IsActive = true;
                    await _db.SaveChangesAsync();
                }
            }

            InvalidateCache(provider);
            return true;
        }

        public async Task<bool> TestApiKeyAsync(string provider, string rawKey)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
                return false;

            var configuredBaseUrl = _configuration[$"{provider}:BaseUrl"];
            var strategy = _strategyFactory.GetStrategy(provider);

            try
            {
                var client = _httpClientFactory.CreateClient();
                using var request = strategy.CreateTestPingRequest(rawKey, configuredBaseUrl);

                var response = await client.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Provider} API key test ping failed.", provider);
                return false;
            }
        }

        public async Task<bool> TestGroqKeyAsync(string rawKey)
        {
            return await TestApiKeyAsync("Groq", rawKey);
        }

        public async Task RecordKeyUsageAsync(string provider = "Groq")
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<EdCoDbContext>();
                var activeKey = await db.AiApiKeys.FirstOrDefaultAsync(k => k.Provider == provider && k.IsActive);
                if (activeKey != null)
                {
                    activeKey.LastUsedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update LastUsedAt timestamp for provider {Provider}", provider);
                try
                {
                    using var errScope = _scopeFactory.CreateScope();
                    var errorLogService = errScope.ServiceProvider.GetService<IErrorLogService>();
                    if (errorLogService != null)
                    {
                        await errorLogService.LogErrorAsync(ex, source: "API", logLevel: "Warning", customMessage: $"Failed to update AI key usage timestamp for provider {provider}: {ex.Message}");
                    }
                }
                catch
                {
                    // Ignore secondary logging failures
                }
            }
        }

        public void InvalidateCache(string provider = "Groq")
        {
            var cacheKey = $"AiApiKey_Active_{provider}";
            _cache.Remove(cacheKey);
        }

        private static string MaskKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (key.Length <= 12) return key.Substring(0, Math.Min(4, key.Length)) + "****";
            return $"{key.Substring(0, 8)}...{key.Substring(key.Length - 4)}";
        }
    }
}
