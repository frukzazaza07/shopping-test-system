using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace StockCart.Infrastructure.Persistence;

/// <summary>
/// Readiness: the DB is reachable AND the schema is fully migrated.
/// New pods therefore receive no traffic until the migration Job has finished.
/// </summary>
internal sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(ct))
                return HealthCheckResult.Unhealthy("Database unreachable.");

            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).Count();
            return pending == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{pending} pending migration(s).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
