
namespace Marketplace.Web.Models;

public record Order(
    Guid Id,
    Guid UserId,
    string DeliveryAddress,
    string Status,
    decimal Total,
    DateTime CreatedAt,
    List<OrderItem> Items);

public record OrderItem(
    Guid Id,
    Guid ProductId,
    string NameSnapshot,
    decimal PriceSnapshot,
    int Quantity);
