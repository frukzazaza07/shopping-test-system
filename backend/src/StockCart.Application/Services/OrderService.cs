using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StockCart.Application.Abstractions;
using StockCart.Application.Dtos;
using StockCart.Application.Exceptions;
using StockCart.Domain.Entities;

namespace StockCart.Application.Services;

public sealed partial class OrderService(
    IProductRepository products,
    IStockRepository stocks,
    IOrderRepository orders,
    IUnitOfWork unitOfWork,
    IStockGate stockGate,
    ILogger<OrderService> logger)
{
    private const int MaxLines = 100;
    private const int MaxQuantityPerLine = 1000;

    /// <param name="idempotencyKey">
    /// Optional. Retrying with the same key returns the order created by the first call
    /// instead of creating (and charging stock for) a second one.
    /// </param>
    public async Task<CreateOrderResult> CreateAsync(CreateOrderRequest request, string? idempotencyKey, CancellationToken ct)
    {
        var lines = NormalizeAndValidate(request);
        ValidateIdempotencyKey(idempotencyKey);

        if (idempotencyKey is not null && await orders.GetByIdempotencyKeyAsync(idempotencyKey, ct) is { } previous)
            return new CreateOrderResult(ToDto(previous), Replayed: true);

        // Prices and deduction rules always come from the DB - never trust the client.
        var productIds = lines.Keys.ToList();
        var productMap = (await products.GetByIdsWithDeductionsAsync(productIds, ct)).ToDictionary(p => p.Id);

        var missing = productIds.Where(id => !productMap.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            throw new NotFoundException($"Product(s) not found: {string.Join(", ", missing)}.");

        // Sum the stock needed per stock-product across ALL lines.
        // e.g. "Cola" x2 + "Burger Combo (incl. Cola)" x1 => Cola stock needs 3.
        var required = new Dictionary<int, (int Quantity, string Name)>();
        foreach (var (productId, quantity) in lines)
        {
            var product = productMap[productId];
            if (product.Deductions.Count == 0)
                throw new ValidationException($"'{product.ProductName}' is not available for sale.");

            foreach (var d in product.Deductions)
            {
                var name = d.DeductProduct?.ProductName ?? product.ProductName;
                var current = required.GetValueOrDefault(d.DeductProductId, (Quantity: 0, Name: name));
                required[d.DeductProductId] = (current.Quantity + d.DeductQuantity * quantity, current.Name);
            }
        }

        // Gate (Redis): reject hopeless requests before they wait on a Postgres row lock.
        // "Unavailable" (Redis down / not loaded) simply skips the gate - Postgres alone decides.
        var quantities = required.ToDictionary(r => r.Key, r => r.Value.Quantity);
        var gate = await stockGate.TryReserveAsync(quantities, ct);
        if (gate is { Status: StockGateStatus.Insufficient, InsufficientProductId: { } soldOutId })
            throw new InsufficientStockException(soldOutId, required[soldOutId].Name);

        var committed = false;
        try
        {
            await using var tx = await unitOfWork.BeginTransactionAsync(ct);
            try
            {
                // Postgres is the source of truth: even after the gate said yes, every unit is
                // re-checked here, so a wrong Redis counter can never cause overselling.
                // Deduct in ascending product id order so concurrent orders always lock rows
                // in the same order -> no deadlocks.
                foreach (var (stockProductId, need) in required.OrderBy(r => r.Key))
                {
                    // UPDATE ... SET stock_remain = stock_remain - @qty WHERE product_id = @id AND stock_remain >= @qty
                    // The UPDATE takes a row lock; a concurrent order on the same product waits, then
                    // re-evaluates the WHERE against the committed value, so stock can never go negative.
                    // The lock lives in Postgres, so this holds no matter how many API replicas are running.
                    var deducted = await stocks.TryDeductAsync(stockProductId, need.Quantity, ct);
                    if (!deducted)
                        throw new InsufficientStockException(stockProductId, need.Name);
                }

                var order = BuildOrder(request, lines, productMap, idempotencyKey);
                orders.Add(order);
                await unitOfWork.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);
                committed = true;
                logger.LogInformation("Order {OrderNo} created ({Lines} lines, total {Total})",
                    order.OrderNo, order.Details.Count, order.TotalAmount);
                return new CreateOrderResult(ToDto(order, productMap), Replayed: false);
            }
            catch (DuplicateKeyException) when (idempotencyKey is not null)
            {
                // The same key raced us (double click / client retry hitting another replica) and won.
                // Undo our stock deduction and return the winner's order.
                await tx.RollbackAsync(CancellationToken.None);
                var winner = await orders.GetByIdempotencyKeyAsync(idempotencyKey, ct)
                             ?? throw new InvalidOperationException("Idempotent order vanished.");
                return new CreateOrderResult(ToDto(winner), Replayed: true);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            // Whatever made the DB not commit (out of stock, duplicate key, crash, cancellation):
            // give the reserved units back to the gate so its counters follow Postgres.
            if (gate.Status == StockGateStatus.Reserved && !committed)
                await stockGate.ReleaseAsync(quantities);
        }
    }

    public async Task<OrderDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(id, ct)
                    ?? throw new NotFoundException($"Order '{id}' not found.");
        return ToDto(order);
    }

    /// <summary>Validates the request and merges duplicate product lines.</summary>
    private static Dictionary<int, int> NormalizeAndValidate(CreateOrderRequest request)
    {
        if (!Enum.IsDefined(request.PaymentMethod))
            throw new ValidationException("Invalid payment method.");

        if (request.Items is null || request.Items.Count == 0)
            throw new ValidationException("Order must contain at least one item.");

        if (request.Items.Count > MaxLines)
            throw new ValidationException($"Order cannot contain more than {MaxLines} lines.");

        var lines = new Dictionary<int, int>();
        foreach (var item in request.Items)
        {
            if (item.ProductId <= 0)
                throw new ValidationException("Invalid product id.");
            if (item.Quantity is <= 0 or > MaxQuantityPerLine)
                throw new ValidationException($"Quantity must be between 1 and {MaxQuantityPerLine}.");

            lines[item.ProductId] = lines.GetValueOrDefault(item.ProductId) + item.Quantity;
        }

        if (lines.Values.Any(q => q > MaxQuantityPerLine))
            throw new ValidationException($"Quantity must be between 1 and {MaxQuantityPerLine}.");

        return lines;
    }

    private static void ValidateIdempotencyKey(string? key)
    {
        if (key is not null && !IdempotencyKeyRegex().IsMatch(key))
            throw new ValidationException("Idempotency-Key must be 8-64 characters of [A-Za-z0-9_-].");
    }

    private static Order BuildOrder(
        CreateOrderRequest request, Dictionary<int, int> lines, Dictionary<int, Product> productMap, string? idempotencyKey)
    {
        var now = DateTime.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNo = $"ORD-{now:yyyyMMddHHmmss}-{RandomNumberGenerator.GetHexString(4)}",
            PaymentMethod = request.PaymentMethod,
            CreatedAt = now,
            IdempotencyKey = idempotencyKey
        };

        foreach (var (productId, quantity) in lines)
        {
            var product = productMap[productId];
            order.Details.Add(new OrderDetail
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = product.ProductPriceUnit,
                LineTotal = product.ProductPriceUnit * quantity
            });
        }

        order.TotalAmount = order.Details.Sum(d => d.LineTotal);
        order.TotalQuantity = order.Details.Sum(d => d.Quantity);
        return order;
    }

    /// <summary>Maps an order loaded with Details.Product.</summary>
    private static OrderDto ToDto(Order order) => ToDto(
        order,
        order.Details.Where(d => d.Product is not null).Select(d => d.Product!).DistinctBy(p => p.Id).ToDictionary(p => p.Id));

    private static OrderDto ToDto(Order order, IReadOnlyDictionary<int, Product> productMap) => new(
        order.Id,
        order.OrderNo,
        order.TotalAmount,
        order.TotalQuantity,
        order.PaymentMethod,
        order.CreatedAt,
        order.Details
            .OrderBy(d => d.Id)
            .Select(d =>
            {
                productMap.TryGetValue(d.ProductId, out var p);
                return new OrderDetailDto(d.ProductId, p?.ProductCode ?? "", p?.ProductName ?? "", d.Quantity, d.UnitPrice, d.LineTotal);
            })
            .ToList());

    [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$")]
    private static partial Regex IdempotencyKeyRegex();
}
