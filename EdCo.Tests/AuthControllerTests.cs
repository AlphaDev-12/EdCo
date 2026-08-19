using System;
using System.Threading.Tasks;
using EdCo.API.Controllers;
using EdCo.API.DTOs;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace EdCo.Tests
{
    public class AuthControllerTests
    {
        private EdCoDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<EdCoDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new EdCoDbContext(options);
        }

        private Mock<UserManager<AppUser>> GetMockUserManager()
        {
            var store = new Mock<IUserStore<AppUser>>();
            return new Mock<UserManager<AppUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        }

        private Mock<SignInManager<AppUser>> GetMockSignInManager(UserManager<AppUser> userManager)
        {
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
            return new Mock<SignInManager<AppUser>>(userManager, contextAccessor.Object, claimsFactory.Object, null!, null!, null!, null!);
        }

        [Fact]
        public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var mockUserManager = GetMockUserManager();
            var mockSignInManager = GetMockSignInManager(mockUserManager.Object);
            var mockAudit = new Mock<IAuditLogService>();
            var mockConfig = new Mock<IConfiguration>();

            var controller = new AuthController(
                mockConfig.Object,
                mockUserManager.Object,
                mockSignInManager.Object,
                dbContext,
                mockAudit.Object
            );

            // Act
            var result = await controller.RefreshToken(new RefreshTokenRequest { RefreshToken = "non_existent_token" });

            // Assert
            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.NotNull(unauthorized);
        }

        [Fact]
        public async Task RefreshToken_WithValidToken_RotatesTokenAndReturnsNewPair()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var user = new AppUser
            {
                Id = "test_user_id",
                UserName = "test@edco.com",
                Email = "test@edco.com",
                FullName = "Test Student"
            };
            dbContext.Users.Add(user);

            // Generate raw token and store its SHA256 hash in DB
            var rawRefreshToken = "valid_raw_refresh_token_string_123456789";
            string tokenHash;
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(rawRefreshToken);
                tokenHash = Convert.ToBase64String(sha256.ComputeHash(bytes));
            }

            var refreshToken = new RefreshToken
            {
                TokenHash = tokenHash,
                UserId = user.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = "127.0.0.1"
            };
            dbContext.RefreshTokens.Add(refreshToken);
            await dbContext.SaveChangesAsync();

            var mockUserManager = GetMockUserManager();
            mockUserManager.Setup(m => m.GetRolesAsync(It.IsAny<AppUser>()))
                           .ReturnsAsync(new[] { "Student" });

            var mockSignInManager = GetMockSignInManager(mockUserManager.Object);
            var mockAudit = new Mock<IAuditLogService>();

            var inMemorySettings = new System.Collections.Generic.Dictionary<string, string?> {
                {"Jwt:Key", "Test_SecretKey_Minimum32CharsLong_EdCo_2026!"},
                {"Jwt:Issuer", "https://edco-api.production.com"},
                {"Jwt:Audience", "https://edco-app.production.com"}
            };
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();

            var controller = new AuthController(
                config,
                mockUserManager.Object,
                mockSignInManager.Object,
                dbContext,
                mockAudit.Object
            );

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

            // Act
            var result = await controller.RefreshToken(new RefreshTokenRequest { RefreshToken = rawRefreshToken });

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);

            var updatedOldToken = await dbContext.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash);
            Assert.NotNull(updatedOldToken?.RevokedAt);
            Assert.NotNull(updatedOldToken?.ReplacedByTokenHash);
        }
    }
}
