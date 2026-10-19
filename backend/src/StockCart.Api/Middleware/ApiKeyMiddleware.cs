using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace StockCart.Api.Middleware;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";
    public string ApiKey { get; set; } = string.Empty;
}

/// <summary>
/// Only callers holding the shared secret (our Next.js server) can use /api/*.
/// The key lives only in server-side env vars, so it is never shipped to the browser.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
{
    public const string HeaderName = "X-Api-Key";
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(options.Value.ApiKey);

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var provided = context.Request.Headers[HeaderName].ToString();
            // Constant-time comparison to avoid timing attacks.
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), _expected))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { title = "Unauthorized", status = 401 });
                return;
            }
        }

        await next(context);
    }
}
