using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using StockCart.Api;
using StockCart.Api.Controllers;
using StockCart.Api.Middleware;
using StockCart.Application;
using StockCart.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Secrets come from environment variables (.env / k8s Secret), never from source code.
builder.Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .Validate(o => o.ApiKey.Length >= 32, "Security:ApiKey must be configured (at least 32 characters).")
    .ValidateOnStart();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.AddDevOpenApi(); // Development only

// CORS: only our Next.js origin may call the API from a browser.
// (In this setup the browser never calls the API directly - Next.js calls it server-side -
// but the policy is still locked down as defence in depth.)
const string corsPolicy = "frontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy(corsPolicy, p => p
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST")
    .WithHeaders("Content-Type", ApiKeyMiddleware.HeaderName, OrdersController.IdempotencyHeader)));

builder.WebHost.ConfigureKestrel(o =>
{
    o.AddServerHeader = false;
    o.Limits.MaxRequestBodySize = 64 * 1024;
});

// On SIGTERM (k8s scale-down / rolling update) let in-flight orders finish before exiting.
builder.Services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(20));

var app = builder.Build();

// `dotnet StockCart.Api.dll --migrate`: apply migrations + seed, then exit.
// Run once per deploy (k8s Job / compose "migrate" service) instead of in every replica.
if (args.Contains("--migrate"))
{
    await app.Services.MigrateDatabaseAsync();
    return;
}

app.UseExceptionHandler();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers.XFrameOptions = "DENY";
    await next();
});
app.UseDevSwagger(); // Development only: /swagger (outside /api, so not behind the API key)
app.UseCors(corsPolicy);
app.UseMiddleware<ApiKeyMiddleware>();

// Liveness: the process is up (never touches the DB, so a DB blip doesn't restart every pod).
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
// Readiness: DB reachable + schema migrated -> only then does the pod receive traffic.
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(StockCart.Infrastructure.DependencyInjection.ReadyTag) });
app.MapControllers();

await app.RunAsync();
