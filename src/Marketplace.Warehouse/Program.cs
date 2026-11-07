using Marketplace.Warehouse.Data;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Marketplace.Shared;
using Marketplace.Warehouse.DTOs;
using Marketplace.Warehouse.Models;
using Marketplace.Shared.Events;
using Marketplace.Warehouse.Consumers;
using MassTransit;

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

    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<OrderCompletedConsumer>();
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host("localhost", "/", h =>
            {
                h.Username("guest");
                h.Password("guest");
            });
            cfg.ConfigureEndpoints(context);
        });
    });

    builder.Services.AddDbContext<WarehouseDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));


    

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();
    // app.UseHttpsRedirection();
    app.MapHealthChecks("/health");

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        db.Database.Migrate();
        DbSeeder.Seed(db);
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
    reservationsApi.MapPost("/", async (
        CreateReservationRequest request,
        WarehouseDbContext db,
        CancellationToken cancellationToken) =>
    {
        if (request.OrderId == Guid.Empty)
            return Results.BadRequest("OrderId is required");

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest("Order must contain at least one item");

        if (request.Items.Any(i => i.ProductId == Guid.Empty || i.Quantity <= 0))
            return Results.BadRequest("ProductId and Quantity must be valid");

        // Объединяем повторяющиеся товары.
        var groupedItems = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(i => (long)i.Quantity)
            })
            .OrderBy(i => i.ProductId)
            .ToList();

        if (groupedItems.Any(i => i.Quantity > int.MaxValue))
            return Results.BadRequest("Requested quantity is too large");

        var normalizedItems = groupedItems
            .Select(i => new ReservationItemRequest(
                i.ProductId,
                (int)i.Quantity))
            .ToList();

        // Проверяем, совпадают ли товары существующего резерва
        // с повторным запросом.
        IResult ExistingReservationResult(Reservation existing)
        {
            var sameItems =
                existing.Items.Count == normalizedItems.Count &&
                existing.Items.All(saved =>
                    normalizedItems.Any(requested =>
                        requested.ProductId == saved.ProductId &&
                        requested.Quantity == saved.Quantity));

            if (!sameItems)
                return Results.Conflict(
                    "Reservation already exists with different items");

            return Results.Ok(new ReservationResponse(
                existing.Id,
                existing.OrderId,
                existing.Status.ToString(),
                existing.CreatedAt));
        }

        var existingReservation = await db.Reservations
            .AsNoTracking()
            .Include(r => r.Items)
            .FirstOrDefaultAsync(
                r => r.OrderId == request.OrderId,
                cancellationToken);

        if (existingReservation is not null)
            return ExistingReservationResult(existingReservation);

        try
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(cancellationToken);

            // Повторная проверка внутри транзакции.
            existingReservation = await db.Reservations
                .AsNoTracking()
                .Include(r => r.Items)
                .FirstOrDefaultAsync(
                    r => r.OrderId == request.OrderId,
                    cancellationToken);

            if (existingReservation is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return ExistingReservationResult(existingReservation);
            }

            var reservation = new Reservation
            {
                OrderId = request.OrderId
            };

            foreach (var item in normalizedItems)
            {
                // Атомарно уменьшаем Available и увеличиваем Reserved.
                // Условие не позволяет уйти в отрицательный остаток.
                var updated = await db.StockItems
                    .Where(s =>
                        s.ProductId == item.ProductId &&
                        s.Available >= item.Quantity)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                s => s.Available,
                                s => s.Available - item.Quantity)
                            .SetProperty(
                                s => s.Reserved,
                                s => s.Reserved + item.Quantity),
                        cancellationToken);

                if (updated != 1)
                {
                    var stock = await db.StockItems
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            s => s.ProductId == item.ProductId,
                            cancellationToken);

                    await transaction.RollbackAsync(cancellationToken);

                    if (stock is null)
                        return Results.Conflict(
                            $"Stock for product {item.ProductId} not found");

                    return Results.Conflict(
                        $"Insufficient stock for product {item.ProductId}. " +
                        $"Available: {stock.Available}, requested: {item.Quantity}");
                }

                reservation.Items.Add(new ReservationItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity
                });
            }

            db.Reservations.Add(reservation);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Results.Created(
                $"/api/reservations/{reservation.Id}",
                new ReservationResponse(
                    reservation.Id,
                    reservation.OrderId,
                    reservation.Status.ToString(),
                    reservation.CreatedAt));
        }
        catch (DbUpdateException)
        {
            // Возможна гонка двух запросов с одним OrderId.
            // Уникальный индекс позволит сохранить только один резерв.
            db.ChangeTracker.Clear();

            var existing = await db.Reservations
                .AsNoTracking()
                .Include(r => r.Items)
                .FirstOrDefaultAsync(
                    r => r.OrderId == request.OrderId,
                    cancellationToken);

            if (existing is null)
                throw;

            return ExistingReservationResult(existing);
        }
    });


    // Освободить резерв (компенсация)
    reservationsApi.MapPost("/{id:guid}/release", async (
        Guid id,
        WarehouseDbContext db,
        CancellationToken cancellationToken) =>
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        // Блокируем строку резерва до завершения транзакции.
        var reservation = await db.Reservations
            .FromSqlInterpolated(
                $"SELECT * FROM \"Reservations\" WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (reservation is null)
            return Results.NotFound();

        // Повторное освобождение безопасно.
        if (reservation.Status == ReservationStatus.Released)
        {
            await transaction.CommitAsync(cancellationToken);
            return Results.NoContent();
        }

        // Проданный товар нельзя вернуть через отмену резерва.
        if (reservation.Status == ReservationStatus.Confirmed)
        {
            return Results.Conflict(
                "Confirmed reservation cannot be released");
        }

        var items = await db.ReservationItems
            .AsNoTracking()
            .Where(i => i.ReservationId == id)
            .OrderBy(i => i.ProductId)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            // Возвращаем товар из Reserved в Available.
            var updated = await db.StockItems
                .Where(s =>
                    s.ProductId == item.ProductId &&
                    s.Reserved >= item.Quantity)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            s => s.Reserved,
                            s => s.Reserved - item.Quantity)
                        .SetProperty(
                            s => s.Available,
                            s => s.Available + item.Quantity),
                    cancellationToken);

            if (updated != 1)
            {
                throw new InvalidOperationException(
                    $"Cannot release reservation {id}: " +
                    $"inconsistent stock for product {item.ProductId}");
            }
        }

        reservation.Status = ReservationStatus.Released;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

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