namespace Marketplace.Delivery.Models;

public sealed class Delivery
{
    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ReservationId { get; private set; }

    public string Address { get; private set; } = string.Empty;

    public DeliveryStatus Status { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    private Delivery()
    {
    }

    public Delivery(Guid orderId, Guid reservationId, string address)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId cannot be empty.");

        if (reservationId == Guid.Empty)
            throw new ArgumentException("ReservationId cannot be empty.");

        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Delivery address is required.");

        address = address.Trim();

        if (address.Length > 500)
            throw new ArgumentException("Delivery address cannot exceed 500 characters.");

        Id = Guid.NewGuid();
        OrderId = orderId;
        ReservationId = reservationId;
        Address = address;
        Status = DeliveryStatus.Created;

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public bool TryChangeStatus(DeliveryStatus newStatus)
    {
        bool isAllowed = Status switch
        {
            DeliveryStatus.Created =>
                newStatus is DeliveryStatus.Preparing or DeliveryStatus.Cancelled,

            DeliveryStatus.Preparing =>
                newStatus is DeliveryStatus.Shipped or DeliveryStatus.Cancelled,

            DeliveryStatus.Shipped =>
                newStatus == DeliveryStatus.Delivered,

            _ => false
        };

        if (!isAllowed)
            return false;

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;

        return true;
    }
}