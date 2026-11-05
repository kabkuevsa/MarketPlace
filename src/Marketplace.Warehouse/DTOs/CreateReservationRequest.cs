namespace Marketplace.Warehouse.DTOs;

public record CreateReservationRequest(
    Guid OrderId,
    List<ReservationItemRequest> Items);

public record ReservationItemRequest(
    Guid ProductId,
    int Quantity);