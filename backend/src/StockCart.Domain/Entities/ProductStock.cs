namespace StockCart.Domain.Entities;

public class ProductStock
{
    /// <summary>Primary key and FK to product (1:1).</summary>
    public int ProductId { get; set; }
    public int StockRemain { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Product? Product { get; set; }
}
