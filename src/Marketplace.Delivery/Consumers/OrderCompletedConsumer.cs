using DeliveryEntity = Marketplace.Delivery.Models.Delivery;
using Marketplace.Delivery.Data;
using Marketplace.Shared.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Delivery.Consumers;

public sealed class OrderCompletedConsumer(
    DeliveryDbContext dbContext,
    ILogger<OrderCompletedConsumer> logger)
    : IConsumer<OrderCompletedEvent>
{
    public async Task Consume(ConsumeContext<OrderCompletedEvent> context)
    {
        var message = context.Message;

        if (message.OrderId == Guid.Empty)
            throw new InvalidOperationException("OrderId cannot be empty.");

        if (message.ReservationId == Guid.Empty)
            throw new InvalidOperationException("ReservationId cannot be empty.");

        if (string.IsNullOrWhiteSpace(message.DeliveryAddress))
            throw new InvalidOperationException("Delivery address is missing.");

        var alreadyExists = await dbContext.Deliveries
            .AnyAsync(
                x => x.OrderId == message.OrderId,
                context.CancellationToken);

        if (alreadyExists)
        {
            logger.LogInformation(
                "Delivery for order {OrderId} already exists. Skipping.",
                message.OrderId);

            return;
        }

        var delivery = new DeliveryEntity(
            message.OrderId,
            message.ReservationId,
            message.DeliveryAddress);

        dbContext.Deliveries.Add(delivery);

        try
        {
            await dbContext.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation(
                "Delivery {DeliveryId} created for order {OrderId}.",
                delivery.Id,
                delivery.OrderId);
        }
        catch (DbUpdateException exception)
        {
            dbContext.Entry(delivery).State = EntityState.Detached;

            var duplicateExists = await dbContext.Deliveries
                .AnyAsync(
                    x => x.OrderId == message.OrderId,
                    context.CancellationToken);

            if (duplicateExists)
            {
                logger.LogInformation(
                    "Duplicate delivery event for order {OrderId} ignored.",
                    message.OrderId);

                return;
            }

            logger.LogError(
                exception,
                "Could not create delivery for order {OrderId}.",
                message.OrderId);

            throw;
        }
    }
}