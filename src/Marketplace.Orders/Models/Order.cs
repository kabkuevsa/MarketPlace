namespace Marketplace.Orders.Models;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<OrderItem> Items { get; set; } = new();
    public Guid? ReservationId { get; set; }  // id резерва на складе (для компенсации)
    public Guid? PaymentId { get; set; }       // id платежа (для рефанда)
}