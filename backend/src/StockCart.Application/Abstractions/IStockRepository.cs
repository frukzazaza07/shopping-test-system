namespace StockCart.Application.Abstractions;

public interface IStockRepository
{
    /// <summary>
    /// Atomically deducts <paramref name="quantity"/> from a product's stock.
    /// Returns false when the remaining stock is not enough (nothing is changed).
    /// Must run inside a transaction: the row stays locked until commit/rollback.
    /// </summary>
    Task<bool> TryDeductAsync(int productId, int quantity, CancellationToken ct);
}
