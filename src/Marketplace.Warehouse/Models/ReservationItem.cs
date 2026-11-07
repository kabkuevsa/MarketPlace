namespace Marketplace.Warehouse.Models;

public class ReservationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReservationId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public Reservation Reservation { get; set; } = null!;
}