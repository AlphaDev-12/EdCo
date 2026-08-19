using System.Threading.Tasks;
using EdCo.Core.Middleware;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EdCo.Tests
{
    public class SecurityHeadersMiddlewareTests
    {
        [Fact]
        public async Task InvokeAsync_AppendsAllRequiredSecurityHeaders()
        {
            // Arrange
            HttpContext context = new DefaultHttpContext();
            RequestDelegate next = (ctx) => Task.CompletedTask;
            var middleware = new SecurityHeadersMiddleware(next);

            // Act
            context.Request.Scheme = "https";
            await middleware.InvokeAsync(context);

            // Assert
            var headers = context.Response.Headers;

            Assert.True(headers.ContainsKey("X-Frame-Options"));
            Assert.Equal("SAMEORIGIN", headers["X-Frame-Options"]);

            Assert.True(headers.ContainsKey("X-Content-Type-Options"));
            Assert.Equal("nosniff", headers["X-Content-Type-Options"]);

            Assert.True(headers.ContainsKey("Referrer-Policy"));
            Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);

            Assert.True(headers.ContainsKey("X-XSS-Protection"));
            Assert.Equal("1; mode=block", headers["X-XSS-Protection"]);

            Assert.True(headers.ContainsKey("Permissions-Policy"));
            Assert.Contains("camera=(self)", headers["Permissions-Policy"].ToString());

            Assert.True(headers.ContainsKey("Strict-Transport-Security"));
            Assert.Contains("max-age=31536000", headers["Strict-Transport-Security"].ToString());

            Assert.True(headers.ContainsKey("Content-Security-Policy"));
            Assert.Contains("default-src 'self'", headers["Content-Security-Policy"].ToString());
        }
    }
}
