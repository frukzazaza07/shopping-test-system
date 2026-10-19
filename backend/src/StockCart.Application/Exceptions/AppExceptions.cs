namespace StockCart.Application.Exceptions;

public abstract class AppException(string message) : Exception(message);

/// <summary>400 - request is invalid.</summary>
public sealed class ValidationException(string message) : AppException(message);

/// <summary>404 - resource not found.</summary>
public sealed class NotFoundException(string message) : AppException(message);

/// <summary>Raised by the persistence layer when a unique constraint is violated.</summary>
public sealed class DuplicateKeyException(string? constraintName, Exception inner)
    : Exception($"Unique constraint '{constraintName}' violated.", inner)
{
    public string? ConstraintName { get; } = constraintName;
}

/// <summary>409 - not enough stock to fulfil the order.</summary>
public sealed class InsufficientStockException(int productId, string productName)
    : AppException($"Insufficient stock for '{productName}'.")
{
    public int ProductId { get; } = productId;
}
