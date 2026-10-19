using Microsoft.EntityFrameworkCore;
using StockCart.Application.Abstractions;

namespace StockCart.Infrastructure.Persistence.Repositories;

internal sealed class StockRepository(AppDbContext db) : IStockRepository
{
    /// <summary>
    /// Translates to a single parameterized statement:
    /// <code>
    /// UPDATE product_stock SET stock_remain = stock_remain - @qty, updated_at = @now
    /// WHERE product_id = @id AND stock_remain >= @qty
    /// </code>
    /// Why not SELECT ... FOR UPDATE and then UPDATE? This does the check and the write in one
    /// round trip, still takes the same row lock (held until the surrounding transaction ends),
    /// and in READ COMMITTED Postgres re-checks the WHERE clause against the latest committed
    /// row once a concurrent writer commits - so two orders can never both take the last unit.
    /// </summary>
    public async Task<bool> TryDeductAsync(int productId, int quantity, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var affected = await db.ProductStocks
            .Where(s => s.ProductId == productId && s.StockRemain >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.StockRemain, x => x.StockRemain - quantity)
                .SetProperty(x => x.UpdatedAt, now), ct);

        return affected == 1;
    }
}
