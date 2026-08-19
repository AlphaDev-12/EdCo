using System;
using System.Linq;
using System.Threading.Tasks;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EdCo.Tests
{
    public class SoftDeleteQueryFilterTests
    {
        private EdCoDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<EdCoDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new EdCoDbContext(options);
        }

        [Fact]
        public async Task SoftDelete_ItemIsMarkedIsDeletedAndExcludedFromQueries()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var grade = new GradeLevel { Name = "Test Grade 9", TierPrice = 10.00m, IsActive = true };
            dbContext.GradeLevels.Add(grade);
            await dbContext.SaveChangesAsync();

            // Act
            dbContext.GradeLevels.Remove(grade);
            await dbContext.SaveChangesAsync();

            // Assert
            var activeGrades = await dbContext.GradeLevels.ToListAsync();
            Assert.Empty(activeGrades);

            // Verify with IgnoreQueryFilters
            var deletedGrades = await dbContext.GradeLevels.IgnoreQueryFilters().ToListAsync();
            Assert.Single(deletedGrades);
            Assert.True(deletedGrades[0].IsDeleted);
            Assert.NotNull(deletedGrades[0].DeletedAt);
        }
    }
}
