namespace StockCart.Domain.Entities;

/// <summary>Groups products for display on the frontend.</summary>
public class ProductCategory
{
    public int Id { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
