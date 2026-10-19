using Microsoft.EntityFrameworkCore;
using StockCart.Application.Abstractions;
using StockCart.Domain.Entities;

namespace StockCart.Infrastructure.Persistence.Repositories;

// All queries go through LINQ -> EF Core emits parameterized SQL (no string concatenation => no SQL injection).
internal sealed class ProductRepository(AppDbContext db) : IProductRepository
{
    public async Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(CancellationToken ct) =>
        await db.ProductCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> GetAllWithStockAsync(CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Include(p => p.Stock)
            .Include(p => p.Deductions)
            .OrderBy(p => p.CategoryId).ThenBy(p => p.ProductCode)
            .AsSplitQuery()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> GetByIdsWithDeductionsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.Deductions).ThenInclude(d => d.DeductProduct)
            .ToListAsync(ct);
}
