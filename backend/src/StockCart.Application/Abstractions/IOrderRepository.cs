using StockCart.Domain.Entities;

namespace StockCart.Application.Abstractions;

public interface IOrderRepository
{
    void Add(Order order);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct);
}
