namespace Marketplace.Shared.Events;

/// <summary>Заказ успешно завершён (оплачен и зарезервирован). 
/// Склад должен подтвердить резерв — списать товар.</summary>
public record OrderCompletedEvent(
    Guid OrderId,
    Guid ReservationId);

/// <summary>Заказ отменён из-за ошибки. 
/// Склад должен освободить резерв, платежи — вернуть деньги.</summary>
public record OrderCancelledEvent(
    Guid OrderId,
    Guid? ReservationId,
    Guid? PaymentId,
    string Reason);

/// <summary>Склад успешно зарезервировал товары.</summary>
public record StockReservedEvent(
    Guid OrderId,
    Guid ReservationId);

/// <summary>Склад не смог зарезервировать (недостаточно остатков).</summary>
public record StockReservationFailedEvent(
    Guid OrderId,
    string Reason);