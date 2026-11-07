using Marketplace.Shared;
using Marketplace.Warehouse.Models;

namespace Marketplace.Warehouse.Data;

public static class DbSeeder
{
    public static void Seed(WarehouseDbContext db)
    {
        if (db.StockItems.Any()) return;

        db.StockItems.AddRange(
            new StockItem { ProductId = DemoData.SmartphoneId, Available = 10, Reserved = 0 },
            new StockItem { ProductId = DemoData.LaptopId, Available = 5, Reserved = 0 },
            new StockItem { ProductId = DemoData.LampId, Available = 20, Reserved = 0 },
            new StockItem { ProductId = DemoData.TeaSetId, Available = 15, Reserved = 0 }
        );

        db.SaveChanges();
    }
}