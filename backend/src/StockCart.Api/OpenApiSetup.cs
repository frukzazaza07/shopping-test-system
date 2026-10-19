using Microsoft.OpenApi;
using StockCart.Api.Middleware;

namespace StockCart.Api;

/// <summary>
/// Swagger UI for local development only (ASPNETCORE_ENVIRONMENT=Development).
/// Everything lives under /swagger so a single nginx location (docker-compose.dev.yml) can expose it:
///   /swagger                  UI
///   /swagger/v1/swagger.json  OpenAPI document
///   /swagger/try/api/...      "Try it out" calls, proxied by nginx to /api/... (X-Api-Key still required)
/// </summary>
public static class OpenApiSetup
{
    private const string DocumentPath = "/swagger/{documentName}/swagger.json";
    private const string TryItOutPrefix = "/swagger/try";
    private const string ApiKeyScheme = "ApiKey";

    public static void AddDevOpenApi(this WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment()) return;

        builder.Services.AddOpenApi(o => o.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Stock & Cart API";
            document.Servers = [new OpenApiServer { Url = TryItOutPrefix, Description = "via nginx (dev)" }];

            // "Authorize" button: paste API_KEY from .env
            document.AddComponent<IOpenApiSecurityScheme>(ApiKeyScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = ApiKeyMiddleware.HeaderName,
                Description = "Value of API_KEY in .env"
            });
            document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(ApiKeyScheme, document)] = [] }];
            return Task.CompletedTask;
        }));
    }

    public static void UseDevSwagger(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        app.MapOpenApi(DocumentPath);
        app.UseSwaggerUI(o =>
        {
            o.RoutePrefix = "swagger";
            o.SwaggerEndpoint("/swagger/v1/swagger.json", "Stock & Cart API v1");
            o.DocumentTitle = "Stock & Cart API (dev)";
        });
    }
}
