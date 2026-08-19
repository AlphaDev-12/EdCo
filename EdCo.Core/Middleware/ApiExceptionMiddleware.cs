using System.Net;
using System.Text.Json;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdCo.Core.Middleware;

public class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred while processing request {Path} with trace ID {TraceId}",
                context.Request.Path, context.TraceIdentifier);

            var isApiRequest = context.Request.Path.StartsWithSegments("/api") ||
                               (context.Request.Headers["Accept"].ToString().Contains("application/json") && 
                                !context.Request.Headers["Accept"].ToString().Contains("text/html"));
            var source = isApiRequest ? "API" : "AdminPortal";

            try
            {
                var errorLogService = context.RequestServices.GetService<IErrorLogService>();
                if (errorLogService != null)
                {
                    await errorLogService.LogErrorAsync(ex, source: source, httpContext: context, logLevel: "Error");
                }
            }
            catch (Exception logEx)
            {
                _logger.LogWarning(logEx, "Failed to persist error to ErrorLogService database table.");
            }

            if (isApiRequest || context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                await HandleExceptionAsync(context, ex);
            }
            else
            {
                // Re-throw so standard MVC exception handler (/Home/Error/500) can render HTML error page
                throw;
            }
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Title = "An unhandled error occurred",
            Detail = "An unexpected error occurred processing your request. Please try again or contact support.",
            Instance = context.Request.Path
        };

        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(problemDetails, options);
        await context.Response.WriteAsync(json);
    }
}
