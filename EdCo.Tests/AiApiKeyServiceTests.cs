using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EdCo.Tests
{
    public class AiApiKeyServiceTests
    {
        private EdCoDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<EdCoDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new EdCoDbContext(options);
        }

        [Fact]
        public void EncryptionService_EncryptAndDecrypt_ReturnsOriginalPlainText()
        {
            // Arrange
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Jwt:Key", "TestMasterKeyForEncryptionWhichIsLongEnough12345!" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
            var encryptionService = new AiApiKeyEncryptionService(config);

            string originalApiKey = "gsk_TestApiKeyString1234567890abcdefghijklmnopqrstuvwxyz";

            // Act
            string cipherText = encryptionService.Encrypt(originalApiKey);
            string decryptedText = encryptionService.Decrypt(cipherText);

            // Assert
            Assert.NotEqual(originalApiKey, cipherText);
            Assert.Equal(originalApiKey, decryptedText);
        }

        [Fact]
        public async Task GetActiveKeyAsync_NoDbKey_ReturnsAppsettingsFallback()
        {
            // Arrange
            var db = GetInMemoryDbContext();
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Jwt:Key", "TestMasterKeyForEncryptionWhichIsLongEnough12345!" },
                { "Groq:ApiKey", "gsk_appsettings_fallback_key_123" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
            var encryptionService = new AiApiKeyEncryptionService(config);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockStrategyFactory = new Mock<EdCo.Core.Interfaces.IAiProviderStrategyFactory>();

            var apiKeyService = new AiApiKeyService(
                db, 
                encryptionService, 
                cache, 
                config, 
                mockHttpClientFactory.Object, 
                mockScopeFactory.Object,
                mockStrategyFactory.Object,
                NullLogger<AiApiKeyService>.Instance);

            // Act
            string activeKey = await apiKeyService.GetActiveKeyAsync("Groq");

            // Assert
            Assert.Equal("gsk_appsettings_fallback_key_123", activeKey);
        }

        [Fact]
        public async Task AddKeyAsync_SetsNewActiveKeyAndInvalidatesCache()
        {
            // Arrange
            var db = GetInMemoryDbContext();
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Jwt:Key", "TestMasterKeyForEncryptionWhichIsLongEnough12345!" },
                { "Groq:ApiKey", "gsk_appsettings_fallback_key_123" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
            var encryptionService = new AiApiKeyEncryptionService(config);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockStrategyFactory = new Mock<EdCo.Core.Interfaces.IAiProviderStrategyFactory>();

            var apiKeyService = new AiApiKeyService(
                db, 
                encryptionService, 
                cache, 
                config, 
                mockHttpClientFactory.Object, 
                mockScopeFactory.Object,
                mockStrategyFactory.Object,
                NullLogger<AiApiKeyService>.Instance);

            string rawKey = "gsk_new_database_groq_api_key_999";

            // Act
            var createdKey = await apiKeyService.AddKeyAsync("Production Groq Key", rawKey, setAsActive: true, provider: "Groq");
            string resolvedKey = await apiKeyService.GetActiveKeyAsync("Groq");

            // Assert
            Assert.True(createdKey.IsActive);
            Assert.Equal("Groq", createdKey.Provider);
            Assert.Equal("gsk_new_..._999", createdKey.MaskedKey);
            Assert.Equal(rawKey, resolvedKey);
        }

        [Fact]
        public async Task SetActiveKeyAsync_DeactivatesPreviousKey()
        {
            // Arrange
            var db = GetInMemoryDbContext();
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Jwt:Key", "TestMasterKeyForEncryptionWhichIsLongEnough12345!" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
            var encryptionService = new AiApiKeyEncryptionService(config);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockStrategyFactory = new Mock<EdCo.Core.Interfaces.IAiProviderStrategyFactory>();

            var apiKeyService = new AiApiKeyService(
                db, 
                encryptionService, 
                cache, 
                config, 
                mockHttpClientFactory.Object, 
                mockScopeFactory.Object,
                mockStrategyFactory.Object,
                NullLogger<AiApiKeyService>.Instance);

            var key1 = await apiKeyService.AddKeyAsync("Key 1", "gsk_key_number_1_1111111111", setAsActive: true);
            var key2 = await apiKeyService.AddKeyAsync("Key 2", "gsk_key_number_2_2222222222", setAsActive: false);

            // Act
            await apiKeyService.SetActiveKeyAsync(key2.Id, "Groq");
            string activeKey = await apiKeyService.GetActiveKeyAsync("Groq");

            // Assert
            Assert.Equal("gsk_key_number_2_2222222222", activeKey);

            var updatedKey1 = await db.AiApiKeys.FindAsync(key1.Id);
            var updatedKey2 = await db.AiApiKeys.FindAsync(key2.Id);

            Assert.False(updatedKey1!.IsActive);
            Assert.True(updatedKey2!.IsActive);
        }

        [Fact]
        public async Task DeepInfra_AddAndResolveActiveKey_ReturnsDeepInfraKey()
        {
            // Arrange
            var db = GetInMemoryDbContext();
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Jwt:Key", "TestMasterKeyForEncryptionWhichIsLongEnough12345!" },
                { "DeepInfra:ApiKey", "deepinfra_fallback_key_123" },
                { "AiSettings:ActiveProvider", "DeepInfra" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
            var encryptionService = new AiApiKeyEncryptionService(config);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockStrategyFactory = new Mock<EdCo.Core.Interfaces.IAiProviderStrategyFactory>();

            var apiKeyService = new AiApiKeyService(
                db, 
                encryptionService, 
                cache, 
                config, 
                mockHttpClientFactory.Object, 
                mockScopeFactory.Object,
                mockStrategyFactory.Object,
                NullLogger<AiApiKeyService>.Instance);

            // Act
            string fallbackKey = await apiKeyService.GetActiveKeyAsync("DeepInfra");
            var createdKey = await apiKeyService.AddKeyAsync("Production DeepInfra Key", "deepinfra_secret_prod_key_999", setAsActive: true, provider: "DeepInfra");
            string resolvedKey = await apiKeyService.GetActiveKeyAsync("DeepInfra");
            string defaultProviderKey = await apiKeyService.GetActiveKeyAsync(); // should default to DeepInfra

            // Assert
            Assert.Equal("deepinfra_fallback_key_123", fallbackKey);
            Assert.True(createdKey.IsActive);
            Assert.Equal("DeepInfra", createdKey.Provider);
            Assert.Equal("deepinfra_secret_prod_key_999", resolvedKey);
            Assert.Equal("deepinfra_secret_prod_key_999", defaultProviderKey);
        }

        [Fact]
        public async Task SetActiveProviderAsync_SwitchesActiveProvider()
        {
            // Arrange
            var db = GetInMemoryDbContext();
            var inMemoryConfig = new Dictionary<string, string?>
            {
                { "Jwt:Key", "TestMasterKeyForEncryptionWhichIsLongEnough12345!" },
                { "AiSettings:ActiveProvider", "Groq" }
            };
            var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
            var encryptionService = new AiApiKeyEncryptionService(config);
            var cache = new MemoryCache(new MemoryCacheOptions());
            var mockHttpClientFactory = new Mock<IHttpClientFactory>();

            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            var mockStrategyFactory = new Mock<EdCo.Core.Interfaces.IAiProviderStrategyFactory>();

            var apiKeyService = new AiApiKeyService(
                db, 
                encryptionService, 
                cache, 
                config, 
                mockHttpClientFactory.Object, 
                mockScopeFactory.Object,
                mockStrategyFactory.Object,
                NullLogger<AiApiKeyService>.Instance);

            // Act
            string initialProvider = await apiKeyService.GetActiveProviderAsync();
            await apiKeyService.SetActiveProviderAsync("DeepInfra");
            string newProvider = await apiKeyService.GetActiveProviderAsync();

            // Assert
            Assert.Equal("Groq", initialProvider);
            Assert.Equal("DeepInfra", newProvider);
        }
    }
}
