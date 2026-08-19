using Microsoft.AspNetCore.HttpOverrides;
using System.Text;
using EdCo.Core.Data;
using EdCo.Core.Entities;
using EdCo.Core.Extensions;
using EdCo.Core.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EdCo.Core.Interfaces;
using EdCo.API.Services;
using Serilog;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Prometheus;
using Microsoft.OpenApi;

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
    .ConfigureResource(resource => resource.AddService("EdCo.API"))
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

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var secretKey = jwtSettings["Key"] ?? jwtSettings["SecretKey"];

if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(secretKey) || 
        secretKey.Contains("SecretKeyMustBeSetInAppSettings") || 
        secretKey.Length < 32)
    {
        throw new InvalidOperationException("CRITICAL SECURITY ERROR: Production deployment requires a secure, non-default Jwt:Key with at least 32 characters configured via environment variable or secret manager.");
    }
}
else
{
    secretKey ??= "Development_SecretKey_Minimum32CharsLong_EdCo_2026!";
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"] ?? "https://edco-api.production.com",
        ValidAudience = jwtSettings["Audience"] ?? "https://edco-app.production.com",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// Configure CORS with explicit allowed origins and dynamic development support
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() 
    ?? new[] { 
        "https://edco.com", 
        "https://admin.edco.com", 
        "http://localhost:3000", 
        "http://localhost:5000", 
        "http://localhost:5001", 
        "http://localhost:5075",
        "http://localhost:8081",
        "http://localhost:8082",
        "http://localhost:19006",
        "http://127.0.0.1:8081",
        "http://127.0.0.1:19006",
        "http://192.168.1.154:8081",
        "http://192.168.1.154:19006",
        "http://192.168.1.154:5075"
    };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => {
            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
            else
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
        });
});

// Configure Distributed Cache (Redis) with local Memory Cache fallback
var redisConnStr = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConnStr))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnStr;
        options.InstanceName = "EdCoCache_";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, EdCo.Core.Services.RedisCacheService>();

// Configure Rate Limiter for AI endpoints (10 req/min per student) and Auth endpoints (5 req/min per IP)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AiEndpointsPolicy", httpContext =>
    {
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                     ?? httpContext.Connection.RemoteIpAddress?.ToString() 
                     ?? "anonymous";

        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    });
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

builder.Services.AddHttpClient<IGeminiVisionService, GeminiVisionService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
}).AddStandardResilienceHandler(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    options.Retry.ShouldHandle = new Polly.PredicateBuilder<HttpResponseMessage>()
        .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
        .Handle<HttpRequestException>();
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITutorEngineService, QuantitativeTutorService>();
builder.Services.AddSingleton<ILocalFileStorageService, EdCo.Core.Services.LocalFileStorageService>();
builder.Services.AddScoped<IAuditLogService, EdCo.Core.Services.AuditLogService>();
builder.Services.AddScoped<IErrorLogService, EdCo.Core.Services.ErrorLogService>();
builder.Services.AddScoped<IAiCreditGuardService, EdCo.Core.Services.AiCreditGuardService>();
builder.Services.AddSingleton<IAiApiKeyEncryptionService, EdCo.Core.Services.AiApiKeyEncryptionService>();
builder.Services.AddSingleton<IAiProviderStrategy, EdCo.Core.Services.Providers.GroqProviderStrategy>();
builder.Services.AddSingleton<IAiProviderStrategy, EdCo.Core.Services.Providers.DeepInfraProviderStrategy>();
builder.Services.AddSingleton<IAiProviderStrategyFactory, EdCo.Core.Services.Providers.AiProviderStrategyFactory>();
builder.Services.AddScoped<IAiApiKeyService, EdCo.Core.Services.AiApiKeyService>();
builder.Services.AddSingleton<IAiGradingPromptBuilder, AiGradingPromptBuilder>();
builder.Services.AddSingleton<IAiResponseParserService, AiResponseParserService>();
builder.Services.AddScoped<IAiGradingService, AiGradingService>();
builder.Services.AddScoped<IAiRubricService, AiRubricService>();
builder.Services.AddScoped<IOcrExtractionService, OcrExtractionService>();
builder.Services.AddScoped<ICurriculumService, CurriculumService>();
builder.Services.AddSingleton<IGradingJobQueue, EdCo.API.Services.GradingJobQueue>();
builder.Services.AddHostedService<EdCo.API.Services.GradingBackgroundWorkerService>();
builder.Services.AddHostedService<EdCo.Core.Services.AuditLogCleanupHostedService>();
builder.Services.AddHostedService<EdCo.Core.Services.RefreshTokenCleanupHostedService>();

// Guardian WhatsApp Chatbot & Subscription Services
builder.Services.AddHttpClient("WhatsApp");
builder.Services.AddHttpClient("Ecocash");
builder.Services.AddHttpClient("Paynow");
builder.Services.AddScoped<EdCo.API.Services.WhatsApp.IWhatsAppService, EdCo.API.Services.WhatsApp.WhatsAppService>();
builder.Services.AddScoped<EdCo.API.Services.WhatsApp.IEcocashService, EdCo.API.Services.WhatsApp.EcocashService>();
builder.Services.AddScoped<EdCo.API.Services.WhatsApp.IGuardianWhatsAppBotService, EdCo.API.Services.WhatsApp.GuardianWhatsAppBotService>();

builder.Services.AddControllers(options =>
{
    options.Filters.Add<EdCo.Core.Filters.ModelStateValidationFilter>();
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EdCo API", Version = "v1" });
    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT Bearer token."
    };

    c.AddSecurityDefinition("Bearer", bearerScheme);

    var schemeRef = new OpenApiSecuritySchemeReference("Bearer");
    c.AddSecurityRequirement((doc) => new OpenApiSecurityRequirement
    {
        { schemeRef, new List<string>() }
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Fail-fast environment secret validation for Production and Staging
if (!builder.Environment.IsDevelopment())
{
    var missingKeys = new List<string>();
    if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:Key"]) && string.IsNullOrWhiteSpace(builder.Configuration["Jwt:SecretKey"]))
        missingKeys.Add("Jwt:Key");
    if (string.IsNullOrWhiteSpace(builder.Configuration["Groq:ApiKey"]))
        missingKeys.Add("Groq:ApiKey");
    if (string.IsNullOrWhiteSpace(builder.Configuration["Gemini:ApiKey"]))
        missingKeys.Add("Gemini:ApiKey");
        
    if (missingKeys.Count > 0)
    {
        throw new InvalidOperationException($"CRITICAL SECURITY ERROR: Staging/Production startup halted due to missing secrets: {string.Join(", ", missingKeys)}");
    }
}

builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();

// Seed mock curriculum data in Development environment
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<EdCoDbContext>();
            await dbContext.Database.MigrateAsync();
            await EdCo.Core.Data.DatabaseSeeder.SeedAsync(dbContext);
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding mock curriculum data.");
        }
    }
}

app.UseForwardedHeaders();

// Global Exception Middleware
app.UseMiddleware<ApiExceptionMiddleware>();

// Serilog HTTP Request Logging
app.UseSerilogRequestLogging();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");

app.UseRateLimiter();

app.UseMiddleware<EdCo.Core.Middleware.SecurityHeadersMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpMetrics();

app.MapEdCoHealthChecks();
app.MapMetrics();
app.MapControllers();

app.Run();
