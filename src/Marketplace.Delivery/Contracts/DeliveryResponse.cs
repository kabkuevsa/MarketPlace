using DeliveryEntity = Marketplace.Delivery.Models.Delivery;

namespace Marketplace.Delivery.Contracts;

public sealed record DeliveryResponse(
    Guid Id,
    Guid OrderId,
    Guid ReservationId,
    string Address,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static DeliveryResponse FromEntity(DeliveryEntity delivery)
    {
        return new DeliveryResponse(
            delivery.Id,
            delivery.OrderId,
            delivery.ReservationId,
            delivery.Address,
            delivery.Status.ToString(),
            delivery.CreatedAt,
            delivery.UpdatedAt);
    }
}