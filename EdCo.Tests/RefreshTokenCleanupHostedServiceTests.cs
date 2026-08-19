using System;
using System.Linq;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EdCo.Tests
{
    public class RefreshTokenCleanupHostedServiceTests
    {
        private (IServiceProvider serviceProvider, EdCoDbContext dbContext) CreateTestServices()
        {
            var services = new ServiceCollection();
            var dbName = Guid.NewGuid().ToString();

            services.AddDbContext<EdCoDbContext>(options =>
                options.UseInMemoryDatabase(dbName));

            var provider = services.BuildServiceProvider();
            var dbContext = provider.GetRequiredService<EdCoDbContext>();

            return (provider, dbContext);
        }

        [Fact]
        public async Task PerformCleanupAsync_RemovesExpiredAndRevokedTokens_KeepsActiveTokens()
        {
            // Arrange
            var (serviceProvider, dbContext) = CreateTestServices();

            var activeToken = new RefreshToken
            {
                TokenHash = "active_hash_123",
                UserId = "user_1",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            var expiredToken = new RefreshToken
            {
                TokenHash = "expired_hash_456",
                UserId = "user_2",
                ExpiresAt = DateTime.UtcNow.AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddDays(-8)
            };

            var revokedToken = new RefreshToken
            {
                TokenHash = "revoked_hash_789",
                UserId = "user_3",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                RevokedAt = DateTime.UtcNow.AddHours(-2),
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            dbContext.RefreshTokens.AddRange(activeToken, expiredToken, revokedToken);
            await dbContext.SaveChangesAsync();

            var service = new RefreshTokenCleanupHostedService(
                serviceProvider,
                NullLogger<RefreshTokenCleanupHostedService>.Instance);

            // Act
            int deletedCount = await service.PerformCleanupAsync();

            // Assert
            Assert.Equal(2, deletedCount);

            var remainingTokens = await dbContext.RefreshTokens.ToListAsync();
            Assert.Single(remainingTokens);
            Assert.Equal("active_hash_123", remainingTokens[0].TokenHash);
        }
    }
}
