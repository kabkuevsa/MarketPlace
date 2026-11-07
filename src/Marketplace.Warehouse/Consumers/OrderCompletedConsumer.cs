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

    public OrderCompletedConsumer(WarehouseDbContext db, ILogger<OrderCompletedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        _logger.LogInformation("Получено событие OrderCompleted для резерва {ReservationId}",
            context.Message.ReservationId);

        var reservation = await _db.Reservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == context.Message.ReservationId);

        if (reservation is null)
        {
            _logger.LogWarning("Резерв {ReservationId} не найден", context.Message.ReservationId);
            return;
        }

        if (reservation.Status != ReservationStatus.Created)
        {
            _logger.LogWarning("Резерв {ReservationId} уже в статусе {Status}",
                context.Message.ReservationId, reservation.Status);
            return;
        }

        // Списываем товар: резерв снимается, Available не возвращается (товар ушёл клиенту)
        foreach (var item in reservation.Items)
        {
            var stock = await _db.StockItems.FirstOrDefaultAsync(s => s.ProductId == item.ProductId);
            if (stock is not null)
            {
                stock.Reserved -= item.Quantity;
                // Available НЕ увеличиваем — товар продан
            }
        }

        reservation.Status = ReservationStatus.Confirmed;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Резерв {ReservationId} подтверждён, товар списан",
            context.Message.ReservationId);
    }
}