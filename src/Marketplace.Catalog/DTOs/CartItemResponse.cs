namespace Marketplace.Catalog.DTOs;

public record CartItemResponse(
    Guid Id,
    Guid UserId,
    Guid ProductId,
    string ProductName,
    decimal ProductPrice,
    int Quantity,
    decimal TotalPrice);