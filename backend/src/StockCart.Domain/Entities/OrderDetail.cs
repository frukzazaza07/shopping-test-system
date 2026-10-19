namespace StockCart.Domain.Entities;

public class OrderDetail
{
    public long Id { get; set; }
    public Guid OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }

    /// <summary>Price snapshot at the time of sale.</summary>
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public Order? Order { get; set; }
    public Product? Product { get; set; }
}
