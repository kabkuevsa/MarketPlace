using DeliveryEntity = Marketplace.Delivery.Models.Delivery;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Delivery.Data;

public sealed class DeliveryDbContext(
    DbContextOptions<DeliveryDbContext> options)
    : DbContext(options)
{
    public DbSet<DeliveryEntity> Deliveries => Set<DeliveryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var entity = modelBuilder.Entity<DeliveryEntity>();

        entity.HasKey(x => x.Id);

        entity.Property(x => x.Address)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        entity.Property(x => x.CreatedAt).IsRequired();
        entity.Property(x => x.UpdatedAt).IsRequired();

        entity.HasIndex(x => x.OrderId)
            .IsUnique();
    }
}