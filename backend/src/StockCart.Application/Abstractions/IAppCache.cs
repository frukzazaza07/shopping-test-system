namespace StockCart.Application.Abstractions;

/// <summary>
/// Best-effort cache for read-heavy data. Implementations never throw:
/// an unavailable cache simply behaves like a miss and the caller reads Postgres.
/// </summary>
public interface IAppCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
}
