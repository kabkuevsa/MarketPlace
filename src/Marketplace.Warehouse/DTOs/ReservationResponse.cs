namespace Marketplace.Warehouse.DTOs;

public record ReservationResponse(
    Guid Id,
    Guid OrderId,
    string Status,
    DateTime CreatedAt);