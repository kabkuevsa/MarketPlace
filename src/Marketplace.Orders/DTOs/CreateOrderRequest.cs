namespace Marketplace.Orders.DTOs;

public record CreateOrderRequest(
    Guid UserId,
    List<CreateOrderItemRequest> Items);

public record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity);