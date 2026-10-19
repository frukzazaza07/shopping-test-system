namespace StockCart.Application.Dtos;

public sealed record CategoryDto(int Id, string CategoryName, int SortOrder);

public sealed record DeductionDto(int StockProductId, int Quantity);

/// <summary>Cached part of a product (rarely changes). Stock is joined in live at read time.</summary>
public sealed record CatalogProductDto(
    int Id,
    int CategoryId,
    string ProductCode,
    string ProductName,
    decimal ProductPriceUnit,
    bool HasStock,
    IReadOnlyList<DeductionDto> Deductions);

/// <param name="StockRemain">Physical stock of this product (null for bundle-only products).</param>
/// <param name="Available">How many units can be sold right now, computed from the deduction rules.</param>
public sealed record ProductDto(
    int Id,
    int CategoryId,
    string ProductCode,
    string ProductName,
    decimal ProductPriceUnit,
    int? StockRemain,
    int Available,
    IReadOnlyList<DeductionDto> Deductions);
