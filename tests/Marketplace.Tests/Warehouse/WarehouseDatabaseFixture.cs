using Marketplace.Warehouse.Data;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Tests.Warehouse;

public sealed class WarehouseDatabaseFixture : IAsyncLifetime
{
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable(
            "WAREHOUSE_TEST_CONNECTION_STRING")
        ?? "Host=localhost;Port=5445;Database=warehouse_test_db;Username=postgres;Password=postgres";

    public WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(_connectionString)
            .Options;

        return new WarehouseDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();

        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }
}