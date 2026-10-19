using Microsoft.EntityFrameworkCore;
using StockCart.Application.Abstractions;
using StockCart.Domain.Entities;

namespace StockCart.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(AppDbContext db) : IOrderRepository
{
    public void Add(Order order) => db.Orders.Add(order);

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Orders.AsNoTracking()
            .Include(o => o.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Order?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct) =>
        db.Orders.AsNoTracking()
            .Include(o => o.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(o => o.IdempotencyKey == idempotencyKey, ct);
}
