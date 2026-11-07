using Marketplace.Payments.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Payments.Data;

public class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
    {
    }

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.Property(p => p.Status).HasConversion<string>();
            e.HasIndex(p => p.OrderId);
        });
    }
}