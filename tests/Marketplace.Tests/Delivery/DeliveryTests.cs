using DeliveryEntity = Marketplace.Delivery.Models.Delivery;
using Marketplace.Delivery.Models;

namespace Marketplace.Tests.Delivery;

public class DeliveryTests
{
    private static DeliveryEntity CreateDelivery()
    {
        return new DeliveryEntity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Moscow, Example Street 12");
    }

    [Fact]
    public void Constructor_ValidData_CreatesDelivery()
    {
        var orderId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();

        var delivery = new DeliveryEntity(
            orderId,
            reservationId,
            "  Moscow, Example Street 12  ");

        Assert.NotEqual(Guid.Empty, delivery.Id);
        Assert.Equal(orderId, delivery.OrderId);
        Assert.Equal(reservationId, delivery.ReservationId);
        Assert.Equal("Moscow, Example Street 12", delivery.Address);
        Assert.Equal(DeliveryStatus.Created, delivery.Status);
        Assert.Equal(delivery.CreatedAt, delivery.UpdatedAt);
    }

    [Fact]
    public void Constructor_EmptyOrderId_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() =>
            new DeliveryEntity(
                Guid.Empty,
                Guid.NewGuid(),
                "Moscow"));
    }

    [Fact]
    public void Constructor_EmptyReservationId_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() =>
            new DeliveryEntity(
                Guid.NewGuid(),
                Guid.Empty,
                "Moscow"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_InvalidAddress_ThrowsException(string address)
    {
        Assert.Throws<ArgumentException>(() =>
            new DeliveryEntity(
                Guid.NewGuid(),
                Guid.NewGuid(),
                address));
    }

    [Fact]
    public void Constructor_AddressLongerThan500Characters_ThrowsException()
    {
        var address = new string('A', 501);

        Assert.Throws<ArgumentException>(() =>
            new DeliveryEntity(
                Guid.NewGuid(),
                Guid.NewGuid(),
                address));
    }

    [Fact]
    public void TryChangeStatus_CreatedToPreparing_ReturnsTrue()
    {
        var delivery = CreateDelivery();

        Assert.True(delivery.TryChangeStatus(DeliveryStatus.Preparing));
        Assert.Equal(DeliveryStatus.Preparing, delivery.Status);
    }

    [Fact]
    public void TryChangeStatus_PreparingToShipped_ReturnsTrue()
    {
        var delivery = CreateDelivery();

        delivery.TryChangeStatus(DeliveryStatus.Preparing);

        Assert.True(delivery.TryChangeStatus(DeliveryStatus.Shipped));
        Assert.Equal(DeliveryStatus.Shipped, delivery.Status);
    }

    [Fact]
    public void TryChangeStatus_ShippedToDelivered_ReturnsTrue()
    {
        var delivery = CreateDelivery();

        delivery.TryChangeStatus(DeliveryStatus.Preparing);
        delivery.TryChangeStatus(DeliveryStatus.Shipped);

        Assert.True(delivery.TryChangeStatus(DeliveryStatus.Delivered));
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
    }

    [Fact]
    public void TryChangeStatus_CreatedToCancelled_ReturnsTrue()
    {
        var delivery = CreateDelivery();

        Assert.True(delivery.TryChangeStatus(DeliveryStatus.Cancelled));
        Assert.Equal(DeliveryStatus.Cancelled, delivery.Status);
    }

    [Fact]
    public void TryChangeStatus_PreparingToCancelled_ReturnsTrue()
    {
        var delivery = CreateDelivery();

        delivery.TryChangeStatus(DeliveryStatus.Preparing);

        Assert.True(delivery.TryChangeStatus(DeliveryStatus.Cancelled));
        Assert.Equal(DeliveryStatus.Cancelled, delivery.Status);
    }

    [Theory]
    [InlineData(DeliveryStatus.Created, DeliveryStatus.Shipped)]
    [InlineData(DeliveryStatus.Created, DeliveryStatus.Delivered)]
    [InlineData(DeliveryStatus.Created, DeliveryStatus.Created)]
    [InlineData(DeliveryStatus.Preparing, DeliveryStatus.Created)]
    [InlineData(DeliveryStatus.Preparing, DeliveryStatus.Delivered)]
    [InlineData(DeliveryStatus.Shipped, DeliveryStatus.Preparing)]
    [InlineData(DeliveryStatus.Shipped, DeliveryStatus.Cancelled)]
    [InlineData(DeliveryStatus.Delivered, DeliveryStatus.Preparing)]
    [InlineData(DeliveryStatus.Delivered, DeliveryStatus.Cancelled)]
    [InlineData(DeliveryStatus.Cancelled, DeliveryStatus.Preparing)]
    public void TryChangeStatus_InvalidTransition_ReturnsFalse(
        DeliveryStatus initialStatus,
        DeliveryStatus newStatus)
    {
        var delivery = CreateDelivery();

        MoveToStatus(delivery, initialStatus);

        var result = delivery.TryChangeStatus(newStatus);

        Assert.False(result);
        Assert.Equal(initialStatus, delivery.Status);
    }

    private static void MoveToStatus(
        DeliveryEntity delivery,
        DeliveryStatus targetStatus)
    {
        switch (targetStatus)
        {
            case DeliveryStatus.Created:
                break;

            case DeliveryStatus.Preparing:
                delivery.TryChangeStatus(DeliveryStatus.Preparing);
                break;

            case DeliveryStatus.Shipped:
                delivery.TryChangeStatus(DeliveryStatus.Preparing);
                delivery.TryChangeStatus(DeliveryStatus.Shipped);
                break;

            case DeliveryStatus.Delivered:
                delivery.TryChangeStatus(DeliveryStatus.Preparing);
                delivery.TryChangeStatus(DeliveryStatus.Shipped);
                delivery.TryChangeStatus(DeliveryStatus.Delivered);
                break;

            case DeliveryStatus.Cancelled:
                delivery.TryChangeStatus(DeliveryStatus.Cancelled);
                break;
        }
    }
}