using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StockCart.Application.Abstractions;

namespace StockCart.Infrastructure.Redis;

/// <summary>JSON values with a TTL. Any Redis problem is treated as a cache miss.</summary>
internal sealed class RedisAppCache(IConnectionMultiplexer redis, ILogger<RedisAppCache> logger) : IAppCache
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(key);
            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value.ToString());
        }
        catch (Exception ex) when (RedisStockGate.IsRedisFailure(ex) || ex is JsonException)
        {
            logger.LogDebug(ex, "Cache read failed for {Key}", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
    {
        try
        {
            await redis.GetDatabase().StringSetAsync(key, JsonSerializer.Serialize(value), ttl);
        }
        catch (Exception ex) when (RedisStockGate.IsRedisFailure(ex))
        {
            logger.LogDebug(ex, "Cache write failed for {Key}", key);
        }
    }
}
