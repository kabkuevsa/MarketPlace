using Marketplace.Shared.Events;
using Marketplace.Warehouse.Data;
using Marketplace.Warehouse.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Warehouse.Consumers;

public class OrderCompletedConsumer : IConsumer<OrderCompletedEvent>
{
    private readonly WarehouseDbContext _db;
    private readonly ILogger<OrderCompletedConsumer> _logger;

    public OrderCompletedConsumer(
        WarehouseDbContext db,
        ILogger<OrderCompletedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var cancellationToken = context.CancellationToken;
        var reservationId = context.Message.ReservationId;

        await using var transaction =
            await _db.Database.BeginTransactionAsync(cancellationToken);

        // Блокируем резерв, чтобы одновременно не выполнялись
        // подтверждение и освобождение одного и того же резерва.
        var reservation = await _db.Reservations
            .FromSqlInterpolated(
                $"SELECT * FROM \"Reservations\" WHERE \"Id\" = {reservationId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (reservation is null)
        {
            throw new InvalidOperationException(
                $"Reservation {reservationId} was not found");
        }

        // Повторное событие не должно списывать товар второй раз.
        if (reservation.Status == ReservationStatus.Confirmed)
        {
            _logger.LogInformation(
                "Reservation {ReservationId} is already confirmed",
                reservationId);

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (reservation.Status == ReservationStatus.Released)
        {
            throw new InvalidOperationException(
                $"Cannot confirm released reservation {reservationId}");
        }

        var items = await _db.ReservationItems
            .AsNoTracking()
            .Where(i => i.ReservationId == reservationId)
            .OrderBy(i => i.ProductId)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            // Списываем товар из Reserved.
            // Available не увеличиваем: товар продан.
            var updated = await _db.StockItems
                .Where(s =>
                    s.ProductId == item.ProductId &&
                    s.Reserved >= item.Quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        s => s.Reserved,
                        s => s.Reserved - item.Quantity),
                    cancellationToken);

            if (updated != 1)
            {
                throw new InvalidOperationException(
                    $"Cannot confirm reservation {reservationId}: " +
                    $"inconsistent stock for product {item.ProductId}");
            }
        }

        reservation.Status = ReservationStatus.Confirmed;

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Reservation {ReservationId} confirmed successfully. " +
            "Stock items have been written off.",
            reservationId);
    }
}