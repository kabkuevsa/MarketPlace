namespace Marketplace.Orders.Models;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public string NameSnapshot { get; set; } = string.Empty;  // название на момент заказа
    public decimal PriceSnapshot { get; set; }                 // цена на момент заказа
    public int Quantity { get; set; }
    public Order Order { get; set; } = null!;
}