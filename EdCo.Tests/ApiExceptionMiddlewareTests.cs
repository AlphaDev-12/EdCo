using System.Net;
using System.Text.Json;
using EdCo.Core.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EdCo.Tests
{
    public class ApiExceptionMiddlewareTests
    {
        private readonly Mock<ILogger<ApiExceptionMiddleware>> _mockLogger;

        public ApiExceptionMiddlewareTests()
        {
            _mockLogger = new Mock<ILogger<ApiExceptionMiddleware>>();
        }

        [Fact]
        public async Task InvokeAsync_NoException_CallsNextDelegate()
        {
            // Arrange
            bool nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new ApiExceptionMiddleware(next, _mockLogger.Object);
            var context = new DefaultHttpContext();

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.True(nextCalled);
            Assert.Equal(200, context.Response.StatusCode);
        }

        [Fact]
        public async Task InvokeAsync_UnhandledException_Returns500ProblemDetails()
        {
            // Arrange
            RequestDelegate next = (ctx) => throw new InvalidOperationException("Test unhandled exception");

            var middleware = new ApiExceptionMiddleware(next, _mockLogger.Object);
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            context.TraceIdentifier = "trace-id-12345";
            context.Request.Path = "/api/v1/test/fail";

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);
            Assert.Equal("application/problem+json", context.Response.ContentType);

            context.Response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(context.Response.Body);
            var responseText = await reader.ReadToEndAsync();

            var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(responseText, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            Assert.NotNull(problemDetails);
            Assert.Equal(500, problemDetails.Status);
            Assert.Equal("/api/v1/test/fail", problemDetails.Instance);
        }
    }
}
