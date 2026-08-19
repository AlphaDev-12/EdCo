using System;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using EdCo.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EdCo.Tests
{
    public class AiCreditGuardTests
    {
        private EdCoDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<EdCoDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new EdCoDbContext(options);
        }

        [Fact]
        public async Task ReserveHoldingCreditAsync_UnderMonthlyLimit_ReturnsAllowedTrue()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var mockCache = new Mock<ICacheService>();
            var mockLogger = new Mock<ILogger<AiCreditGuardService>>();

            mockCache.Setup(c => c.GetAsync<decimal>(It.IsAny<string>()))
                     .ReturnsAsync(0m);

            var service = new AiCreditGuardService(dbContext, mockCache.Object, mockLogger.Object);

            // Act
            var (allowed, errorMessage) = await service.ReserveHoldingCreditAsync("user_123", 0.10m);

            // Assert
            Assert.True(allowed);
            Assert.Null(errorMessage);
        }

        [Fact]
        public async Task ReserveHoldingCreditAsync_ExceedingLimit_ReturnsAllowedFalse()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var mockCache = new Mock<ICacheService>();
            var mockLogger = new Mock<ILogger<AiCreditGuardService>>();

            mockCache.Setup(c => c.GetAsync<decimal>(It.IsAny<string>()))
                     .ReturnsAsync(0.45m); // $0.45 active holdings out of $0.50 limit

            var service = new AiCreditGuardService(dbContext, mockCache.Object, mockLogger.Object);

            // Act
            var (allowed, errorMessage) = await service.ReserveHoldingCreditAsync("user_123", 0.10m); // Attempt $0.10 -> $0.55 total

            // Assert
            Assert.False(allowed);
            Assert.Contains("limit reached", errorMessage);
        }
    }
}
