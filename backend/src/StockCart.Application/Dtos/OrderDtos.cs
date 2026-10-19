using StockCart.Domain.Enums;

namespace StockCart.Application.Dtos;

public sealed record CreateOrderItem(int ProductId, int Quantity);

public sealed record CreateOrderRequest(PaymentMethod PaymentMethod, IReadOnlyList<CreateOrderItem>? Items);

/// <param name="Replayed">True when the Idempotency-Key matched an existing order (nothing new was created).</param>
public sealed record CreateOrderResult(OrderDto Order, bool Replayed);

public sealed record OrderDetailDto(
    int ProductId,
    string ProductCode,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record OrderDto(
    Guid Id,
    string OrderNo,
    decimal TotalAmount,
    int TotalQuantity,
    PaymentMethod PaymentMethod,
    DateTime CreatedAt,
    IReadOnlyList<OrderDetailDto> Details);
