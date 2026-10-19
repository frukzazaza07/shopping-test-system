using StockCart.Application.Abstractions;

namespace StockCart.Infrastructure.Redis;

public sealed record RedisSettings(string ConnectionString, TimeSpan StockSyncInterval);

/// <summary>Used when Redis is not configured: every read goes to Postgres, no gate.</summary>
internal sealed class NoAppCache : IAppCache
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class => Task.FromResult<T?>(null);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class => Task.CompletedTask;
}

internal sealed class NoStockGate : IStockGate
{
    public Task<StockGateResult> TryReserveAsync(IReadOnlyDictionary<int, int> quantities, CancellationToken ct) =>
        Task.FromResult(StockGateResult.Unavailable);

    public Task ReleaseAsync(IReadOnlyDictionary<int, int> quantities) => Task.CompletedTask;

    public Task<IReadOnlyDictionary<int, int>?> GetStockAsync(IReadOnlyCollection<int> productIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<int, int>?>(null);
}
