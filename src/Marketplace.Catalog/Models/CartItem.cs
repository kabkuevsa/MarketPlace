namespace Marketplace.Catalog.Models;

public class CartItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public Product Product { get; set; } = null!;
}