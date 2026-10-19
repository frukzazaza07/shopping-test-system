using Microsoft.EntityFrameworkCore.Storage;
using StockCart.Application.Abstractions;

namespace StockCart.Infrastructure.Persistence;

internal sealed class EfTransaction(IDbContextTransaction transaction) : IAppTransaction
{
    public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
    public Task RollbackAsync(CancellationToken ct) => transaction.RollbackAsync(ct);
    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
