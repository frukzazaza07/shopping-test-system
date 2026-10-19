using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StockCart.Infrastructure.Persistence;

namespace StockCart.Infrastructure.Redis;

/// <summary>
/// Keeps the Redis counters converging to Postgres (the source of truth):
/// on startup, every <c>Redis:StockSyncSeconds</c>, and right after Redis reconnects (it may have
/// restarted empty). Overwriting with committed DB values can only make a counter equal to or
/// HIGHER than the real remaining stock (orders in flight commit afterwards), and a too-high
/// counter is harmless because Postgres re-checks every order. A too-low counter (e.g. a lost
/// release) is exactly what this sync repairs.
/// </summary>
internal sealed class StockSyncService(
    IServiceScopeFactory scopes,
    RedisStockGate gate,
    IConnectionMultiplexer redis,
    RedisSettings settings,
    ILogger<StockSyncService> logger) : BackgroundService
{
    private readonly SemaphoreSlim _syncNow = new(0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        redis.ConnectionFailed += (_, e) => logger.LogWarning("Redis connection lost ({Failure}); serving from Postgres", e.FailureType);
        redis.ConnectionRestored += (_, _) =>
        {
            logger.LogInformation("Redis connection restored; re-syncing stock");
            _syncNow.Release();
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            await SyncOnceAsync(stoppingToken);
            try
            {
                await _syncNow.WaitAsync(settings.StockSyncInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SyncOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stock = await db.ProductStocks.AsNoTracking().ToDictionaryAsync(s => s.ProductId, s => s.StockRemain, ct);
            if (stock.Count == 0) return;

            await gate.ReplaceAllAsync(stock);
            logger.LogDebug("Synced {Count} stock counters to Redis", stock.Count);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("Stock sync to Redis skipped: {Message}", ex.Message);
        }
    }
}
