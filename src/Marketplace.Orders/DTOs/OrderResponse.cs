namespace Marketplace.Orders.DTOs;

public record OrderResponse(
    Guid Id,
    Guid UserId,
    string Status,
    decimal Total,
    DateTime CreatedAt,
    List<OrderItemResponse> Items);

public record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    string NameSnapshot,
    decimal PriceSnapshot,
    int Quantity);