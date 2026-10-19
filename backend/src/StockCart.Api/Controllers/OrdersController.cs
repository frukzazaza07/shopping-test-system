using Microsoft.AspNetCore.Mvc;
using StockCart.Application.Dtos;
using StockCart.Application.Services;

namespace StockCart.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(OrderService orderService) : ControllerBase
{
    public const string IdempotencyHeader = "Idempotency-Key";

    /// <summary>
    /// Creates an order. Send an <c>Idempotency-Key</c> header to make retries safe:
    /// a repeated key returns the original order (200) instead of creating a new one (201).
    /// </summary>
    [HttpPost]
    [ProducesResponseType<OrderDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<OrderDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> Create(
        CreateOrderRequest request,
        [FromHeader(Name = IdempotencyHeader)] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await orderService.CreateAsync(request, idempotencyKey, ct);
        return result.Replayed
            ? Ok(result.Order)
            : CreatedAtAction(nameof(GetById), new { id = result.Order.Id }, result.Order);
    }

    [HttpGet("{id:guid}")]
    public Task<OrderDto> GetById(Guid id, CancellationToken ct) => orderService.GetByIdAsync(id, ct);
}
