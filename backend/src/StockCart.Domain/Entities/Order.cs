using StockCart.Domain.Enums;

namespace StockCart.Domain.Entities;

public class Order
{
    /// <summary>Random GUID so order URLs cannot be enumerated.</summary>
    public Guid Id { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TotalQuantity { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Client-supplied key (unique) so a retried request never creates a second order.</summary>
    public string? IdempotencyKey { get; set; }

    public ICollection<OrderDetail> Details { get; set; } = new List<OrderDetail>();
}
