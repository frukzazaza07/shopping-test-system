namespace StockCart.Domain.Entities;

/// <summary>
/// Selling 1 unit of <see cref="ProductId"/> deducts <see cref="DeductQuantity"/> units
/// from the stock of <see cref="DeductProductId"/>.
/// A normal product deducts itself x1; a combo deducts each of its components.
/// </summary>
public class ProductDeduction
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int DeductProductId { get; set; }
    public int DeductQuantity { get; set; }

    public Product? Product { get; set; }
    public Product? DeductProduct { get; set; }
}
