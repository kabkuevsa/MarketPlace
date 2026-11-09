namespace Marketplace.Web.Models;

public record Delivery(
    Guid Id,
    Guid OrderId,
    Guid ReservationId,
    string Address,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);