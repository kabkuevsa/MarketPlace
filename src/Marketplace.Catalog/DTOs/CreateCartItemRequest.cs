namespace Marketplace.Catalog.DTOs;

public record CreateCartItemRequest(
    Guid UserId,
    Guid ProductId,
    int Quantity);