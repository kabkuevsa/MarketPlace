using Marketplace.Catalog.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Catalog.Data;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId);
        });

        modelBuilder.Entity<CartItem>(e =>
        {
            e.HasOne(c => c.Product)
                .WithMany()
                .HasForeignKey(c => c.ProductId);
            e.HasIndex(c => new { c.UserId, c.ProductId }).IsUnique();
        });
    }
}