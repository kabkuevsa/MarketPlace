using System.Security.Claims;
using System.Text;
using Marketplace.Orders.Data;
using Marketplace.Orders.DTOs;
using Marketplace.Orders.Models;
using Marketplace.Orders.Services;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

    // JWT: токен выдаёт Identity, проверяем его тем же ключом
    var jwt = builder.Configuration.GetSection("JwtSettings");
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt["Issuer"],
                ValidAudience = jwt["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SecretKey"]!))
            };
        });
    builder.Services.AddAuthorization();

    builder.Services.AddDbContext<OrdersDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

    builder.Services.AddMassTransit(x =>
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            var rabbit = builder.Configuration.GetSection("RabbitMq");
            cfg.Host(rabbit["Host"] ?? "localhost", rabbit["VirtualHost"] ?? "/", h =>
            {
                h.Username(rabbit["Username"] ?? "guest");
                h.Password(rabbit["Password"] ?? "guest");
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
    // app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealthChecks("/health");

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        db.Database.Migrate();
    }

    // Id пользователя берём из JWT, а не из тела запроса
    static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    var ordersApi = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization();

    ordersApi.MapPost("/", async (CreateOrderRequest request, ClaimsPrincipal user, OrderSaga saga) =>
    {
        var userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest("Order must contain at least one item");

        if (request.Items.Any(i => i.ProductId == Guid.Empty || i.Quantity <= 0))
            return Results.BadRequest("ProductId and Quantity must be valid");

        var order = new Order { UserId = userId.Value };
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

    ordersApi.MapGet("/", async (ClaimsPrincipal user, OrdersDbContext db) =>
    {
        var userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();
        var uid = userId.Value;

        var orders = await db.Orders
            .Where(o => o.UserId == uid)
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

    ordersApi.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, OrdersDbContext db) =>
    {
        var userId = GetUserId(user);
        if (userId is null) return Results.Unauthorized();
        var uid = userId.Value;

        var order = await db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == uid);

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
