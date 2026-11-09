using Marketplace.Delivery.Consumers;
using Marketplace.Delivery.Contracts;
using Marketplace.Delivery.Data;
using Marketplace.Delivery.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Host.UseSerilog((_, configuration) =>
    configuration.WriteTo.Console());

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// PostgreSQL
var connectionString =
    builder.Configuration.GetConnectionString("Delivery")
    ?? throw new InvalidOperationException(
        "Delivery database connection string is missing.");

builder.Services.AddDbContext<DeliveryDbContext>(options =>
    options.UseNpgsql(connectionString));

// RabbitMQ
var rabbitMq = builder.Configuration.GetSection("RabbitMq");

var rabbitHost = rabbitMq["Host"] ?? "localhost";
var rabbitVirtualHost = rabbitMq["VirtualHost"] ?? "/";
var rabbitUsername = rabbitMq["Username"] ?? "guest";
var rabbitPassword = rabbitMq["Password"] ?? "guest";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCompletedConsumer>();

    x.UsingRabbitMq((context, configuration) =>
    {
        configuration.Host(
            rabbitHost,
            rabbitVirtualHost,
            host =>
            {
                host.Username(rabbitUsername);
                host.Password(rabbitPassword);
            });

        configuration.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Apply database migrations before the application starts.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<DeliveryDbContext>();

    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok(new
{
    service = "Marketplace.Delivery",
    status = "running"
}));

// Health check
app.MapGet("/health", async (
    DeliveryDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var canConnect = await dbContext.Database
        .CanConnectAsync(cancellationToken);

    return canConnect
        ? Results.Ok(new { status = "healthy" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
});

// Get all deliveries
app.MapGet("/api/deliveries", async (
    DeliveryDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var deliveries = await dbContext.Deliveries
        .AsNoTracking()
        .OrderByDescending(x => x.CreatedAt)
        .ToListAsync(cancellationToken);

    var response = deliveries
        .Select(DeliveryResponse.FromEntity)
        .ToList();

    return Results.Ok(response);
});

// Get delivery by ID
app.MapGet("/api/deliveries/{id:guid}", async (
    Guid id,
    DeliveryDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var delivery = await dbContext.Deliveries
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    return delivery is null
        ? Results.NotFound()
        : Results.Ok(DeliveryResponse.FromEntity(delivery));
});

// Get delivery by order ID
app.MapGet("/api/deliveries/order/{orderId:guid}", async (
    Guid orderId,
    DeliveryDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var delivery = await dbContext.Deliveries
        .AsNoTracking()
        .FirstOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);

    return delivery is null
        ? Results.NotFound()
        : Results.Ok(DeliveryResponse.FromEntity(delivery));
});

// Update delivery status
app.MapPut("/api/deliveries/{id:guid}/status", async (
    Guid id,
    UpdateDeliveryStatusRequest request,
    DeliveryDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Status) ||
        !Enum.TryParse<DeliveryStatus>(
            request.Status,
            ignoreCase: true,
            out var newStatus) ||
        !Enum.IsDefined(typeof(DeliveryStatus), newStatus))
    {
        return Results.BadRequest(new
        {
            message = "Invalid delivery status."
        });
    }

    var delivery = await dbContext.Deliveries
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    if (delivery is null)
        return Results.NotFound();

    // Повторная установка текущего статуса не требует обновления.
    if (delivery.Status != newStatus)
    {
        if (!delivery.TryChangeStatus(newStatus))
        {
            return Results.Conflict(new
            {
                message = $"Cannot change status from {delivery.Status} to {newStatus}."
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    return Results.Ok(DeliveryResponse.FromEntity(delivery));
});

app.Run();