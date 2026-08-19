using System;
using System.Linq;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EdCo.Tests
{
    public class ErrorLogServiceTests
    {
        private EdCoDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<EdCoDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new EdCoDbContext(options);
        }

        [Fact]
        public async Task LogErrorAsync_SavesErrorToDatabase()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var service = new ErrorLogService(context);
            var testException = new InvalidOperationException("Test unhandled exception");

            // Act
            await service.LogErrorAsync(testException, source: "API", logLevel: "Error", customMessage: "Test custom error");

            // Assert
            var log = await context.ErrorLogs.FirstOrDefaultAsync();
            Assert.NotNull(log);
            Assert.Equal("API", log.Source);
            Assert.Equal("Error", log.LogLevel);
            Assert.Equal("Test custom error", log.Message);
            Assert.Contains("InvalidOperationException", log.ExceptionType);
            Assert.False(log.IsResolved);
        }

        [Fact]
        public async Task GetErrorLogsAsync_FiltersBySourceAndResolution()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var service = new ErrorLogService(context);

            context.ErrorLogs.AddRange(
                new AppErrorLog { Source = "API", LogLevel = "Error", Message = "API error 1", IsResolved = false, CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
                new AppErrorLog { Source = "AdminPortal", LogLevel = "Critical", Message = "Admin error 1", IsResolved = true, CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
                new AppErrorLog { Source = "API", LogLevel = "Warning", Message = "API error 2", IsResolved = false, CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            // Act
            var (unresolvedApiErrors, totalCount) = await service.GetErrorLogsAsync(source: "API", isResolved: false);

            // Assert
            Assert.Equal(2, totalCount);
            Assert.All(unresolvedApiErrors, err => Assert.Equal("API", err.Source));
            Assert.All(unresolvedApiErrors, err => Assert.False(err.IsResolved));
        }

        [Fact]
        public async Task ResolveErrorAsync_MarksErrorAsResolvedWithNotes()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var service = new ErrorLogService(context);
            var log = new AppErrorLog { Source = "API", Message = "Database connection error", IsResolved = false };
            context.ErrorLogs.Add(log);
            await context.SaveChangesAsync();

            // Act
            bool result = await service.ResolveErrorAsync(log.Id, "SuperAdmin", "Fixed database string");

            // Assert
            Assert.True(result);
            var updatedLog = await context.ErrorLogs.FindAsync(log.Id);
            Assert.NotNull(updatedLog);
            Assert.True(updatedLog.IsResolved);
            Assert.Equal("SuperAdmin", updatedLog.ResolvedBy);
            Assert.Equal("Fixed database string", updatedLog.ResolutionNotes);
            Assert.NotNull(updatedLog.ResolvedAt);
        }

        [Fact]
        public async Task GetErrorStatsAsync_ReturnsAccurateKPIs()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var service = new ErrorLogService(context);

            context.ErrorLogs.AddRange(
                new AppErrorLog { Source = "API", IsResolved = false, CreatedAt = DateTime.UtcNow },
                new AppErrorLog { Source = "AdminPortal", IsResolved = false, CreatedAt = DateTime.UtcNow },
                new AppErrorLog { Source = "API", IsResolved = true, CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            // Act
            var stats = await service.GetErrorStatsAsync();

            // Assert
            Assert.Equal(2, stats["TotalUnresolved"]);
            Assert.Equal(3, stats["Errors24h"]);
            Assert.Equal(1, stats["ApiErrors"]);
            Assert.Equal(1, stats["AdminErrors"]);
        }

        [Fact]
        public async Task LogErrorAsync_SavesGroqRateLimitErrorToDatabase()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var service = new ErrorLogService(context);
            var groqEx = new EdCo.Core.Exceptions.GroqRateLimitException(
                "Groq API rate limit exceeded. Please try again in 15 seconds.",
                retryAfterSeconds: 15,
                modelName: "qwen/qwen3.6-27b",
                responseBody: "Rate limit reached for model qwen/qwen3.6-27b in 14.5s");

            // Act
            await service.LogErrorAsync(groqEx, source: "Groq", logLevel: "Warning", customMessage: "Groq API Rate Limit (429) hit");

            // Assert
            var log = await context.ErrorLogs.FirstOrDefaultAsync(l => l.Source == "Groq");
            Assert.NotNull(log);
            Assert.Equal("Groq", log.Source);
            Assert.Equal("Warning", log.LogLevel);
            Assert.Equal("Groq API Rate Limit (429) hit", log.Message);
            Assert.Contains("GroqRateLimitException", log.ExceptionType);
            Assert.False(log.IsResolved);
        }
    }
}
