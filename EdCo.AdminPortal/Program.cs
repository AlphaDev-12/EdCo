using Microsoft.AspNetCore.HttpOverrides;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel limits to prevent timeouts on slow network uploads (e.g., base64 camera scans)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MinRequestBodyDataRate = null;
    options.Limits.MaxRequestBodySize = 30 * 1024 * 1024; // 30MB
});

// Configure Serilog for structured logging
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProcessId()
    .Enrich.WithThreadId()
);

// Configure OpenTelemetry for tracing & metrics
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("EdCo.AdminPortal"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation();
        if (!string.IsNullOrEmpty(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var uri))
        {
            tracing.AddOtlpExporter(opt => opt.Endpoint = uri);
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddRuntimeInstrumentation()
               .AddPrometheusExporter();
        if (!string.IsNullOrEmpty(otlpEndpoint) && Uri.TryCreate(otlpEndpoint, UriKind.Absolute, out var uri))
        {
            metrics.AddOtlpExporter(opt => opt.Endpoint = uri);
        }
    });

// Configure Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EdCoDbContext>("Database", tags: new[] { "ready", "db" })
    .AddCheck<EdCo.Core.Health.RedisHealthCheck>("Cache", tags: new[] { "ready", "cache" });

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<EdCoDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    .AddEntityFrameworkStores<EdCoDbContext>()
    .AddDefaultTokenProviders();

// Configure Distributed Cache (Redis) with local Memory Cache fallback
var redisConnStr = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnStr))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnStr;
        options.InstanceName = "EdCoAdminCache_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<EdCo.Core.Interfaces.ICacheService, EdCo.Core.Services.RedisCacheService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<EdCo.Core.Interfaces.IEmailSenderService, EdCo.Core.Services.EmailSenderService>();
builder.Services.AddSingleton<EdCo.Core.Interfaces.IFileSecurityService, EdCo.Core.Services.FileSecurityService>();
builder.Services.AddSingleton<EdCo.Core.Interfaces.ILocalFileStorageService, EdCo.Core.Services.LocalFileStorageService>();
builder.Services.AddScoped<EdCo.Core.Interfaces.IAuditLogService, EdCo.Core.Services.AuditLogService>();
builder.Services.AddScoped<EdCo.Core.Interfaces.IErrorLogService, EdCo.Core.Services.ErrorLogService>();
builder.Services.AddSingleton<EdCo.Core.Interfaces.IAiApiKeyEncryptionService, EdCo.Core.Services.AiApiKeyEncryptionService>();
builder.Services.AddSingleton<EdCo.Core.Interfaces.IAiProviderStrategy, EdCo.Core.Services.Providers.GroqProviderStrategy>();
builder.Services.AddSingleton<EdCo.Core.Interfaces.IAiProviderStrategy, EdCo.Core.Services.Providers.DeepInfraProviderStrategy>();
builder.Services.AddSingleton<EdCo.Core.Interfaces.IAiProviderStrategyFactory, EdCo.Core.Services.Providers.AiProviderStrategyFactory>();
builder.Services.AddScoped<EdCo.Core.Interfaces.IAiApiKeyService, EdCo.Core.Services.AiApiKeyService>();
builder.Services.AddHttpClient();


builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".EdCo.Admin.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction() 
        ? CookieSecurePolicy.Always 
        : CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Home/Error/403";

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
            context.Request.Headers["Accept"].ToString().Contains("application/json") ||
            context.Request.ContentType?.Contains("application/json") == true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return context.Response.WriteAsJsonAsync(new { success = false, message = "Your session has expired. Please log in again." });
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
            context.Request.Headers["Accept"].ToString().Contains("application/json") ||
            context.Request.ContentType?.Contains("application/json") == true)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(new { success = false, message = "Access denied. You do not have permission to perform this action." });
        }
        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = ".EdCo.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthEndpointsPolicy", httpContext =>
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous-auth";

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipAddress,
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
});

builder.Services.AddHttpClient("EdCoApi", client =>
{
    var baseUrl = builder.Configuration["EdCoApi:BaseUrl"] ?? "http://localhost:5075";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(90);
}).AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.All;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = null;
});

builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    if (HttpMethods.IsPost(context.Request.Method))
    {
        Log.Information("POST Request Debug: Scheme={Scheme}, Host={Host}, Path={Path}, Origin={Origin}, Referer={Referer}",
            context.Request.Scheme,
            context.Request.Host,
            context.Request.Path,
            context.Request.Headers["Origin"].ToString(),
            context.Request.Headers["Referer"].ToString());
    }
    await next();
});

// Seed identity roles and default SuperAdmin user
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<EdCoDbContext>();
        await dbContext.Database.MigrateAsync();
        await EdCo.AdminPortal.Data.IdentitySeeder.SeedRolesAndAdminUserAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding Identity roles and admin user.");
    }
}

// Global Exception Telemetry Middleware
app.UseMiddleware<EdCo.Core.Middleware.ApiExceptionMiddleware>();

// Serilog HTTP Request Logging
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error/500");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Custom Status Code Pages (404, 403, 500)
app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");

app.UseStaticFiles();

app.UseMiddleware<EdCo.Core.Middleware.SecurityHeadersMiddleware>();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpMetrics();

app.MapEdCoHealthChecks();
app.MapMetrics();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
