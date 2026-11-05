namespace Marketplace.Warehouse.Models;

/// <summary>Остаток товара: сколько свободно и сколько зарезервировано.</summary>
public class StockItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public int Available { get; set; }
    public int Reserved { get; set; }
}