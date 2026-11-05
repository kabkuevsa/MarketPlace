using Marketplace.Catalog.Models;

namespace Marketplace.Catalog.Data;

public static class DbSeeder
{
    public static void Seed(CatalogDbContext db)
    {
        if (db.Categories.Any()) return;

        var electronics = new Category { Name = "Электроника" };
        var home = new Category { Name = "Дом и сад" };
        db.Categories.AddRange(electronics, home);

        db.Products.AddRange(
            new Product { Category = electronics, Name = "Смартфон X1", Description = "Средний смартфон для учёбы", Price = 25000m },
            new Product { Category = electronics, Name = "Ноутбук Pro", Description = "16 ГБ RAM, SSD 512 ГБ", Price = 75000m },
            new Product { Category = home, Name = "Настольная лампа", Description = "Тёплый свет, 3 режима", Price = 1900m },
            new Product { Category = home, Name = "Кофейный сервиз", Description = "6 чашек, керамика", Price = 4300m }
        );

        db.SaveChanges();
    }
}