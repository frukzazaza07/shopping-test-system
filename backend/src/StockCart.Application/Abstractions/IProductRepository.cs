using StockCart.Domain.Entities;

namespace StockCart.Application.Abstractions;

public interface IProductRepository
{
    Task<IReadOnlyList<ProductCategory>> GetCategoriesAsync(CancellationToken ct);

    /// <summary>All products with stock + deductions (read-only, no tracking).</summary>
    Task<IReadOnlyList<Product>> GetAllWithStockAsync(CancellationToken ct);

    /// <summary>Products with their deduction rules (and the deducted product's name).</summary>
    Task<IReadOnlyList<Product>> GetByIdsWithDeductionsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
}
