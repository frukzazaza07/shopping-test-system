using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using StockCart.Application.Abstractions;

namespace StockCart.Infrastructure.Redis;

/// <summary>
/// Stock counters in Redis, one key per stock product: "{stock}:42" -> units.
/// The "{stock}" hash tag puts every counter in the same Redis Cluster slot, so the multi-key
/// Lua scripts below stay valid if Redis is ever clustered.
/// Counters are a copy of Postgres (see <see cref="StockSyncService"/>), never the other way round.
/// </summary>
internal sealed class RedisStockGate(IConnectionMultiplexer redis, ILogger<RedisStockGate> logger) : IStockGate
{
    // Check ALL keys first, then decrement ALL - atomically (Lua runs single-threaded in Redis).
    // Returns 0 = reserved, i > 0 = KEYS[i] is short, -1 = a key is missing (not synced yet).
    private const string ReserveScript = """
        for i = 1, #KEYS do
          local v = redis.call('GET', KEYS[i])
          if not v then return -1 end
          if tonumber(v) < tonumber(ARGV[i]) then return i end
        end
        for i = 1, #KEYS do
          redis.call('DECRBY', KEYS[i], ARGV[i])
        end
        return 0
        """;

    // Give units back, but never recreate a key that disappeared (it will be re-synced from the DB).
    private const string ReleaseScript = """
        for i = 1, #KEYS do
          if redis.call('EXISTS', KEYS[i]) == 1 then
            redis.call('INCRBY', KEYS[i], ARGV[i])
          end
        end
        return 0
        """;

    public static RedisKey Key(int productId) => $"{{stock}}:{productId}";

    public async Task<StockGateResult> TryReserveAsync(IReadOnlyDictionary<int, int> quantities, CancellationToken ct)
    {
        var ids = quantities.Keys.ToArray();
        try
        {
            var result = (int)await redis.GetDatabase().ScriptEvaluateAsync(
                ReserveScript,
                ids.Select(Key).ToArray(),
                ids.Select(id => (RedisValue)quantities[id]).ToArray());

            return result switch
            {
                0 => StockGateResult.Reserved,
                > 0 => new StockGateResult(StockGateStatus.Insufficient, ids[result - 1]),
                _ => StockGateResult.Unavailable
            };
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            logger.LogDebug(ex, "Stock gate unavailable, falling back to Postgres only");
            return StockGateResult.Unavailable;
        }
    }

    public async Task ReleaseAsync(IReadOnlyDictionary<int, int> quantities)
    {
        var ids = quantities.Keys.ToArray();
        try
        {
            await redis.GetDatabase().ScriptEvaluateAsync(
                ReleaseScript,
                ids.Select(Key).ToArray(),
                ids.Select(id => (RedisValue)quantities[id]).ToArray());
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            // Counter is now lower than Postgres; the next sync corrects it. Never oversells.
            logger.LogWarning("Could not release stock reservation ({Message}); next sync will fix it", ex.Message);
        }
    }

    public async Task<IReadOnlyDictionary<int, int>?> GetStockAsync(IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        var ids = productIds.ToArray();
        try
        {
            var values = await redis.GetDatabase().StringGetAsync(ids.Select(Key).ToArray());
            if (values.Any(v => v.IsNull)) return null;

            var stock = new Dictionary<int, int>(ids.Length);
            for (var i = 0; i < ids.Length; i++) stock[ids[i]] = (int)values[i];
            return stock;
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            logger.LogDebug(ex, "Stock gate unavailable for reads");
            return null;
        }
    }

    /// <summary>Overwrite every counter with the committed Postgres values (MSET).</summary>
    public Task ReplaceAllAsync(IReadOnlyDictionary<int, int> stock) =>
        redis.GetDatabase().StringSetAsync(
            stock.Select(s => new KeyValuePair<RedisKey, RedisValue>(Key(s.Key), s.Value)).ToArray());

    internal static bool IsRedisFailure(Exception ex) => ex is RedisException or TimeoutException;
}
