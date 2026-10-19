using StockCart.Application.Abstractions;
using StockCart.Application.Dtos;

namespace StockCart.Application.Services;

/// <summary>
/// Read side. The catalog (names, prices, deduction rules) is cached; stock is read live from the
/// stock gate counters, so a sale never needs a cache invalidation. If either the cache or the gate
/// is unavailable, everything is read from Postgres. Displayed stock is informational only -
/// the order transaction in Postgres is what decides.
/// </summary>
public sealed class ProductService(IProductRepository products, IAppCache cache, IStockGate stockGate)
{
    private const string CategoriesKey = "catalog:categories:v1";
    private const string ProductsKey = "catalog:products:v1";
    private static readonly TimeSpan CatalogTtl = TimeSpan.FromSeconds(60);

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct)
    {
        if (await cache.GetAsync<List<CategoryDto>>(CategoriesKey, ct) is { } cached)
            return cached;

        var categories = (await products.GetCategoriesAsync(ct))
            .Select(c => new CategoryDto(c.Id, c.CategoryName, c.SortOrder))
            .ToList();
        await cache.SetAsync(CategoriesKey, categories, CatalogTtl, ct);
        return categories;
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(CancellationToken ct)
    {
        var catalog = await cache.GetAsync<List<CatalogProductDto>>(ProductsKey, ct);
        if (catalog is not null)
        {
            var stockIds = catalog.Where(p => p.HasStock).Select(p => p.Id).ToList();
            if (await stockGate.GetStockAsync(stockIds, ct) is { } liveStock)
                return Build(catalog, liveStock);
        }

        // Fallback / cache fill: Postgres is always the authority.
        var all = await products.GetAllWithStockAsync(ct);
        var freshCatalog = all.Select(p => new CatalogProductDto(
                p.Id,
                p.CategoryId,
                p.ProductCode,
                p.ProductName,
                p.ProductPriceUnit,
                p.Stock is not null,
                p.Deductions.Select(d => new DeductionDto(d.DeductProductId, d.DeductQuantity)).ToList()))
            .ToList();
        if (catalog is null)
            await cache.SetAsync(ProductsKey, freshCatalog, CatalogTtl, ct);

        var dbStock = all.Where(p => p.Stock is not null).ToDictionary(p => p.Id, p => p.Stock!.StockRemain);
        return Build(freshCatalog, dbStock);
    }

    private static List<ProductDto> Build(IReadOnlyList<CatalogProductDto> catalog, IReadOnlyDictionary<int, int> stock) =>
        catalog.Select(p => new ProductDto(
                p.Id,
                p.CategoryId,
                p.ProductCode,
                p.ProductName,
                p.ProductPriceUnit,
                p.HasStock ? Math.Max(0, stock.GetValueOrDefault(p.Id)) : null,
                CalculateAvailable(p, stock),
                p.Deductions))
            .ToList();

    /// <summary>Sellable units = the most limiting component: min(stock / qty per unit).</summary>
    private static int CalculateAvailable(CatalogProductDto product, IReadOnlyDictionary<int, int> stock)
    {
        if (product.Deductions.Count == 0) return 0;

        return Math.Max(0, product.Deductions
            .Select(d => stock.TryGetValue(d.StockProductId, out var s) ? s / d.Quantity : 0)
            .Min());
    }
}
