
namespace Marketplace.Orders.DTOs;

public record CreateOrderRequest(
    string DeliveryAddress,
    List<CreateOrderItemRequest> Items);

public record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity);
