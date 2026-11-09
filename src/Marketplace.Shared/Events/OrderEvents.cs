
namespace Marketplace.Shared.Events;

public record OrderCompletedEvent(
    Guid OrderId,
    Guid ReservationId,
    string DeliveryAddress);

public record OrderCancelledEvent(
    Guid OrderId,
    Guid? ReservationId,
    Guid? PaymentId,
    string Reason);

public record StockReservedEvent(
    Guid OrderId,
    Guid ReservationId);

public record StockReservationFailedEvent(
    Guid OrderId,
    string Reason);
