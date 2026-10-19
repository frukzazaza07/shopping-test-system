namespace StockCart.Application.Abstractions;

public interface IUnitOfWork
{
    Task<IAppTransaction> BeginTransactionAsync(CancellationToken ct);

    /// <exception cref="Exceptions.DuplicateKeyException">A unique constraint was violated.</exception>
    Task<int> SaveChangesAsync(CancellationToken ct);
}

public interface IAppTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken ct);
    Task RollbackAsync(CancellationToken ct);
}
