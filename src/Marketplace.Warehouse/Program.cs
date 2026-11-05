using Marketplace.Warehouse.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Marketplace.Shared;
using Marketplace.Warehouse.DTOs;
using Marketplace.Warehouse.Models;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Warehouse service");   // <-- имя сервиса

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHealthChecks();
    builder.Services.AddDbContext<WarehouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    app.UseHttpsRedirection();
    app.MapHealthChecks("/health");

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        db.Database.Migrate();
    }

    var stocksApi = app.MapGroup("/api/stocks").WithTags("Stocks");
    var reservationsApi = app.MapGroup("/api/reservations").WithTags("Reservations");

    // Получить все остатки
    stocksApi.MapGet("/", async (WarehouseDbContext db) =>
    {
        var stocks = await db.StockItems
            .Select(s => new StockResponse(s.Id, s.ProductId, s.Available, s.Reserved))
            .ToListAsync();
        return Results.Ok(stocks);
    });

    // Приёмка товара (увеличивает Available)
    stocksApi.MapPost("/", async (CreateStockRequest request, WarehouseDbContext db) =>
    {
        var stock = await db.StockItems.FirstOrDefaultAsync(s => s.ProductId == request.ProductId);

        if (stock is null)
        {
            stock = new StockItem { ProductId = request.ProductId, Available = request.Quantity, Reserved = 0 };
            db.StockItems.Add(stock);
        }
        else
        {
            stock.Available += request.Quantity;
        }

        await db.SaveChangesAsync();
        return Results.Ok();
    });

    // Создать резерв под заказ
    reservationsApi.MapPost("/", async (CreateReservationRequest request, WarehouseDbContext db) =>
    {
        var reservation = new Reservation { OrderId = request.OrderId };

        foreach (var item in request.Items)
        {
            var stock = await db.StockItems.FirstOrDefaultAsync(s => s.ProductId == item.ProductId);

            if (stock is null)
            {
                return Results.BadRequest($"Stock for product {item.ProductId} not found");
            }

            if (stock.Available < item.Quantity)
            {
                return Results.BadRequest($"Insufficient stock for product {item.ProductId}: available {stock.Available}, requested {item.Quantity}");
            }

            stock.Available -= item.Quantity;
            stock.Reserved += item.Quantity;

            reservation.Items.Add(new ReservationItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity
            });
        }

        db.Reservations.Add(reservation);
        await db.SaveChangesAsync();

        return Results.Created($"/api/reservations/{reservation.Id}",
            new ReservationResponse(reservation.Id, reservation.OrderId, reservation.Status.ToString(), reservation.CreatedAt));
    });

    // Освободить резерв (компенсация)
    reservationsApi.MapPost("/{id:guid}/release", async (Guid id, WarehouseDbContext db) =>
    {
        var reservation = await db.Reservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation is null) return Results.NotFound();

        if (reservation.Status == ReservationStatus.Released)
        {
            return Results.BadRequest("Reservation already released");
        }

        foreach (var item in reservation.Items)
        {
            var stock = await db.StockItems.FirstOrDefaultAsync(s => s.ProductId == item.ProductId);
            if (stock is not null)
            {
                stock.Reserved -= item.Quantity;
                stock.Available += item.Quantity;
            }
        }

        reservation.Status = ReservationStatus.Released;
        await db.SaveChangesAsync();

        return Results.NoContent();
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Warehouse service terminated unexpectedly");   // <-- имя сервиса
}
finally
{
    Log.CloseAndFlush();
}