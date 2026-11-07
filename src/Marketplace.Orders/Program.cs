using Marketplace.Orders.Data;
using Marketplace.Orders.DTOs;
using Marketplace.Orders.Models;
using Marketplace.Orders.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Marketplace.Shared.Events;


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Orders service");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHealthChecks();

    builder.Services.AddDbContext<OrdersDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    builder.Services.AddMassTransit(x =>
    {
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

    builder.Services.AddScoped<OrderSaga>();

    builder.Services.AddHttpClient("Catalog", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Catalog"]!);
    });

    builder.Services.AddHttpClient("Warehouse", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Warehouse"]!);
    });

    builder.Services.AddHttpClient("Payments", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:Payments"]!);
    });

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
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        db.Database.Migrate();
    }

    var ordersApi = app.MapGroup("/api/orders").WithTags("Orders");

    ordersApi.MapPost("/", async (CreateOrderRequest request, OrdersDbContext db, OrderSaga saga) =>
    {
        var order = new Order { UserId = request.UserId };
        var (success, error) = await saga.ExecuteAsync(order, request);

        if (!success)
        {
            return Results.BadRequest(error);
        }

        return Results.Created($"/api/orders/{order.Id}", new OrderResponse(
            order.Id,
            order.UserId,
            order.Status.ToString(),
            order.Total,
            order.CreatedAt,
            order.Items.Select(i => new OrderItemResponse(
                i.Id, i.ProductId, i.NameSnapshot, i.PriceSnapshot, i.Quantity)).ToList()
        ));
    });

    ordersApi.MapGet("/", async (OrdersDbContext db) =>
    {
        var orders = await db.Orders
            .Include(o => o.Items)
            .Select(o => new OrderResponse(
                o.Id,
                o.UserId,
                o.Status.ToString(),
                o.Total,
                o.CreatedAt,
                o.Items.Select(i => new OrderItemResponse(
                    i.Id, i.ProductId, i.NameSnapshot, i.PriceSnapshot, i.Quantity)).ToList()
            ))
            .ToListAsync();
        return Results.Ok(orders);
    });

    ordersApi.MapGet("/{id:guid}", async (Guid id, OrdersDbContext db) =>
    {
        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order is null) return Results.NotFound();

        return Results.Ok(new OrderResponse(
            order.Id,
            order.UserId,
            order.Status.ToString(),
            order.Total,
            order.CreatedAt,
            order.Items.Select(i => new OrderItemResponse(
                i.Id, i.ProductId, i.NameSnapshot, i.PriceSnapshot, i.Quantity)).ToList()
        ));
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Orders service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}