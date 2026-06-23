using Asp.Versioning;
using EventosVivos.API.Configuration;
using EventosVivos.API.Middleware;
using EventosVivos.Application;
using EventosVivos.Application.Common;
using EventosVivos.Infrastructure;
using System.Reflection;
using EventosVivos.Infrastructure.Persistence;
using EventosVivos.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using NLog;
using NLog.Web;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var logger = LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // NLog
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // App layers
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.EnvironmentName);

    // JWT settings
    var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>()!;
    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
    builder.Services.Configure<OAuthSettings>(builder.Configuration.GetSection("OAuth"));
    builder.Services.Configure<BusinessSettings>(builder.Configuration.GetSection("Business"));

    // Authentication & Authorization
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
            };
        });

    builder.Services.AddAuthorization();

    // HTTP client for token validation (Google/Microsoft)
    builder.Services.AddHttpClient();

    // Controllers
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // API versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddMvc();

    // Rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, token) =>
        {
            context.HttpContext.Response.ContentType = "application/problem+json";
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc6585#section-4",
                title = "Too Many Requests",
                status = 429,
                detail = "Rate limit exceeded. Please try again later."
            }, token);
        };

        // Auth endpoints: 10 requests / minute per IP (brute-force protection)
        options.AddPolicy(RateLimitPolicies.Auth, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        // General API: 100 requests / minute per IP
        options.AddPolicy(RateLimitPolicies.Api, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    // CORS
    const string corsPolicy = "FrontendPolicy";
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(o => o.AddPolicy(corsPolicy, p =>
        p.WithOrigins(allowedOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()));

    // Swagger / OpenAPI
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title   = "EventosVivos API",
            Version = "v1",
            Description =
                "REST API para el sistema de gestión y reserva de eventos en vivo.\n\n" +
                "### Autenticación\n" +
                "Usa **Microsoft OAuth2** (Authorization Code Flow). Obtén un JWT desde " +
                "`POST /api/v1/auth/microsoft/exchange` y pásalo como `Bearer <token>` " +
                "en el encabezado `Authorization`.\n\n" +
                "### Paginación\n" +
                "Los listados usan **cursor-based pagination**. El campo `nextCursor` de " +
                "la respuesta se pasa como `cursor` en la siguiente petición.\n\n" +
                "### Idempotencia\n" +
                "Incluye el encabezado `Idempotency-Key: <UUID>` en `POST`/`PUT`/`PATCH` " +
                "para garantizar que peticiones duplicadas devuelvan la misma respuesta."
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile), includeControllerXmlComments: true);

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type        = SecuritySchemeType.Http,
            Scheme      = "bearer",
            BearerFormat = "JWT",
            Description = "JWT obtenido desde `POST /api/v1/auth/microsoft/exchange`."
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    // Auto-migrate and seed
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        await DataSeeder.SeedAsync(db);
    }

    // Middleware pipeline
    app.UseCors(corsPolicy);  // must be first so all responses carry CORS headers
    app.UseRateLimiter();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<IdempotencyMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EventosVivos API v1");
        c.DocumentTitle = "EventosVivos API Docs";
        c.DefaultModelsExpandDepth(1);
        c.DisplayRequestDuration();
    });

    // Serve Angular SPA from wwwroot (populated by Dockerfile's frontend-build stage)
    app.UseDefaultFiles();   // maps / → /index.html
    app.UseStaticFiles();    // serves JS/CSS/assets from wwwroot

    app.MapControllers();

    // Any route not matched by the API falls back to Angular's index.html
    // so the Angular Router can handle client-side navigation
    app.MapFallbackToFile("index.html");

    logger.Info("EventosVivos API starting up");
    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Application startup failed");
    throw;
}
finally
{
    LogManager.Shutdown();
}

public partial class Program { }
