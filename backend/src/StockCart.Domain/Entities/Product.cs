namespace StockCart.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal ProductPriceUnit { get; set; }

    public ProductCategory? Category { get; set; }

    /// <summary>Physical stock of this product. Null for products that only exist as a bundle (e.g. combo sets).</summary>
    public ProductStock? Stock { get; set; }

    /// <summary>What gets deducted from stock when 1 unit of this product is sold.</summary>
    public ICollection<ProductDeduction> Deductions { get; set; } = new List<ProductDeduction>();
}
