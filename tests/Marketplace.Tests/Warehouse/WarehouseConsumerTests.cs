using Marketplace.Shared.Events;
using Marketplace.Warehouse.Consumers;
using Marketplace.Warehouse.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Marketplace.Tests.Warehouse;

public class WarehouseConsumerTests
    : IClassFixture<WarehouseDatabaseFixture>
{
    private readonly WarehouseDatabaseFixture _fixture;

    public WarehouseConsumerTests(WarehouseDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Consume_CreatedReservation_ConfirmsAndWritesOffStock()
    {
        var reservationId = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        await SeedReservationAsync(
            reservationId,
            ReservationStatus.Created,
            (productA, 2, 2),
            (productB, 3, 3));

        await ConsumeAsync(reservationId);

        await using var db = _fixture.CreateDbContext();

        var reservation = await db.Reservations
            .AsNoTracking()
            .SingleAsync(x => x.Id == reservationId);

        var stocks = await db.StockItems
            .AsNoTracking()
            .Where(x => x.ProductId == productA ||
                        x.ProductId == productB)
            .ToDictionaryAsync(x => x.ProductId);

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);

        Assert.Equal(0, stocks[productA].Reserved);
        Assert.Equal(0, stocks[productB].Reserved);

        // Доступное количество не меняется при подтверждении:
        // товар уже был вычтен из Available во время резерва.
        Assert.Equal(10, stocks[productA].Available);
        Assert.Equal(10, stocks[productB].Available);
    }

    [Fact]
    public async Task Consume_AlreadyConfirmedReservation_DoesNotWriteOffAgain()
    {
        var reservationId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await SeedReservationAsync(
            reservationId,
            ReservationStatus.Created,
            (productId, 3, 3));

        await ConsumeAsync(reservationId);
        await ConsumeAsync(reservationId);

        await using var db = _fixture.CreateDbContext();

        var reservation = await db.Reservations
            .SingleAsync(x => x.Id == reservationId);

        var stock = await db.StockItems
            .SingleAsync(x => x.ProductId == productId);

        Assert.Equal(ReservationStatus.Confirmed, reservation.Status);
        Assert.Equal(0, stock.Reserved);
        Assert.Equal(10, stock.Available);
    }

    [Fact]
    public async Task Consume_ReleasedReservation_ThrowsAndDoesNotChangeStock()
    {
        var reservationId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await SeedReservationAsync(
            reservationId,
            ReservationStatus.Released,
            (productId, 2, 2));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ConsumeAsync(reservationId));

        await using var db = _fixture.CreateDbContext();

        var reservation = await db.Reservations
            .AsNoTracking()
            .SingleAsync(x => x.Id == reservationId);

        var stock = await db.StockItems
            .AsNoTracking()
            .SingleAsync(x => x.ProductId == productId);

        Assert.Equal(ReservationStatus.Released, reservation.Status);
        Assert.Equal(2, stock.Reserved);
        Assert.Equal(10, stock.Available);
    }

    [Fact]
    public async Task Consume_ReservationDoesNotExist_ThrowsException()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ConsumeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Consume_InconsistentStock_RollsBackTransaction()
    {
        var reservationId = Guid.NewGuid();

        // Первый продукт будет успешно обновлён до возникновения ошибки.
        // Второго зарезервированного количества недостаточно.
        var prefix = Guid.NewGuid().ToString("N")[..30];

        var productA = Guid.ParseExact(prefix + "01", "N");
        var productB = Guid.ParseExact(prefix + "02", "N");

        // Каждый запуск использует уникальный Id резерва.
        await SeedReservationAsync(
            reservationId,
            ReservationStatus.Created,
            (productA, 2, 2),
            (productB, 1, 0));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ConsumeAsync(reservationId));

        await using var db = _fixture.CreateDbContext();

        var reservation = await db.Reservations
            .AsNoTracking()
            .SingleAsync(x => x.Id == reservationId);

        var stockA = await db.StockItems
            .AsNoTracking()
            .SingleAsync(x => x.ProductId == productA);

        var stockB = await db.StockItems
            .AsNoTracking()
            .SingleAsync(x => x.ProductId == productB);

        // Первое изменение тоже должно откатиться.
        Assert.Equal(ReservationStatus.Created, reservation.Status);
        Assert.Equal(2, stockA.Reserved);
        Assert.Equal(0, stockB.Reserved);
    }

    private async Task ConsumeAsync(Guid reservationId)
    {
        await using var db = _fixture.CreateDbContext();

        var consumer = new OrderCompletedConsumer(
            db,
            NullLogger<OrderCompletedConsumer>.Instance);

        var message = new OrderCompletedEvent(
            Guid.NewGuid(),
            reservationId,
            "Test delivery address");

        var context = new Mock<ConsumeContext<OrderCompletedEvent>>();

        context.SetupGet(x => x.Message).Returns(message);
        context.SetupGet(x => x.CancellationToken)
            .Returns(CancellationToken.None);

        await consumer.Consume(context.Object);
    }

    private async Task SeedReservationAsync(
        Guid reservationId,
        ReservationStatus status,
        params (Guid ProductId, int Quantity, int Reserved)[] products)
    {
        await using var db = _fixture.CreateDbContext();

        var reservation = new Reservation
        {
            Id = reservationId,
            OrderId = Guid.NewGuid(),
            Status = status,
            Items = products.Select(product =>
                new ReservationItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.ProductId,
                    Quantity = product.Quantity
                }).ToList()
        };

        foreach (var product in products)
        {
            db.StockItems.Add(new StockItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.ProductId,
                Available = 10,
                Reserved = product.Reserved
            });
        }

        db.Reservations.Add(reservation);

        await db.SaveChangesAsync();
    }
}