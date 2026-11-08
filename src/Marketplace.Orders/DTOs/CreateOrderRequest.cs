namespace Marketplace.Orders.DTOs;

public record CreateOrderRequest(
    List<CreateOrderItemRequest> Items);

public record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity);