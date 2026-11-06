using Serilog;
using Marketplace.Payments.Data;
using Microsoft.EntityFrameworkCore;
using Marketplace.Payments.DTOs;
using Marketplace.Payments.Models;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting Payments service");   // <-- имя сервиса

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHealthChecks();
    builder.Services.AddDbContext<PaymentsDbContext>(options =>
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
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        db.Database.Migrate();
    }

    var paymentsApi = app.MapGroup("/api/payments").WithTags("Payments");

    paymentsApi.MapPost("/", async (CreatePaymentRequest request, PaymentsDbContext db) =>
    {
        // Эмуляция: 90% платежей проходят успешно
        var isSuccess = Random.Shared.NextDouble() > 0.1;

        var payment = new Payment
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            Status = isSuccess ? PaymentStatus.Completed : PaymentStatus.Failed
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        if (!isSuccess)
        {
            return Results.BadRequest(new PaymentResponse(
                payment.Id, payment.OrderId, payment.Amount,
                payment.Status.ToString(), payment.CreatedAt));
        }

        return Results.Created($"/api/payments/{payment.Id}",
            new PaymentResponse(payment.Id, payment.OrderId, payment.Amount,
                payment.Status.ToString(), payment.CreatedAt));
    });

    paymentsApi.MapPost("/{id:guid}/refund", async (Guid id, PaymentsDbContext db) =>
    {
        var payment = await db.Payments.FindAsync(id);

        if (payment is null) return Results.NotFound();

        if (payment.Status != PaymentStatus.Completed)
        {
            return Results.BadRequest($"Cannot refund payment with status {payment.Status}");
        }

        payment.Status = PaymentStatus.Refunded;
        await db.SaveChangesAsync();

        return Results.NoContent();
    });

    paymentsApi.MapGet("/", async (PaymentsDbContext db) =>
    {
        var payments = await db.Payments
            .Select(p => new PaymentResponse(p.Id, p.OrderId, p.Amount,
                p.Status.ToString(), p.CreatedAt))
            .ToListAsync();
        return Results.Ok(payments);
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Payments service terminated unexpectedly");   // <-- имя сервиса
}
finally
{
    Log.CloseAndFlush();
}