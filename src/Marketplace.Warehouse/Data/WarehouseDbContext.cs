using Marketplace.Warehouse.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Warehouse.Data;

public class WarehouseDbContext : DbContext
{
    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options) : base(options)
    {
    }

    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reservation>(e =>
        {
            e.HasMany(r => r.Items)
                .WithOne(i => i.Reservation)
                .HasForeignKey(i => i.ReservationId);

            e.Property(r => r.Status).HasConversion<string>();
            e.HasIndex(r => r.OrderId).IsUnique();
        });
    }
}