namespace Marketplace.Warehouse.Models;

public class Reservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Created;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<ReservationItem> Items { get; set; } = new();
}