namespace StockCart.Application.Abstractions;

public enum StockGateStatus
{
    /// <summary>Units were reserved in the gate; the DB transaction must still confirm them.</summary>
    Reserved,

    /// <summary>The gate is sure there is not enough stock: reject without touching the DB.</summary>
    Insufficient,

    /// <summary>Gate down or not loaded yet: skip it and let Postgres decide on its own.</summary>
    Unavailable
}

public sealed record StockGateResult(StockGateStatus Status, int? InsufficientProductId = null)
{
    public static readonly StockGateResult Reserved = new(StockGateStatus.Reserved);
    public static readonly StockGateResult Unavailable = new(StockGateStatus.Unavailable);
}

/// <summary>
/// Fast stock pre-check in front of Postgres (Redis counters).
/// It is NOT the source of truth: every order is still re-checked by the conditional UPDATE in
/// the DB transaction, so a wrong counter can never cause overselling. Its job is to turn away
/// requests that cannot succeed before they queue up on the same row lock during a flash sale.
/// </summary>
public interface IStockGate
{
    /// <summary>Atomically checks and reserves all quantities (stockProductId -> units), or nothing.</summary>
    Task<StockGateResult> TryReserveAsync(IReadOnlyDictionary<int, int> quantities, CancellationToken ct);

    /// <summary>Returns a reservation whose DB transaction did not commit. Never throws.</summary>
    Task ReleaseAsync(IReadOnlyDictionary<int, int> quantities);

    /// <summary>Current counters for display, or null when the gate is unavailable or incomplete.</summary>
    Task<IReadOnlyDictionary<int, int>?> GetStockAsync(IReadOnlyCollection<int> productIds, CancellationToken ct);
}
