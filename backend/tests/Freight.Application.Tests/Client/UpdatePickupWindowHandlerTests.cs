using Freight.Application.Client;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Common;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Client;

public sealed class UpdatePickupWindowHandlerTests
{
    private static readonly DateTime BookedAt = new(2026, 1, 1, 6, 0, 0);

    private static Shipment SomeShipment() => Shipment.Book(
        Guid.NewGuid(), Guid.NewGuid(),
        GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
        Capacity.Create(100, 5), TruckType.Refrigerated,
        TimeWindow.Create(BookedAt.AddHours(2), BookedAt.AddHours(4)),
        TimeWindow.Create(BookedAt.AddHours(8), BookedAt.AddHours(10)),
        BookedAt);

    [Fact]
    public async Task HandleAsync_ExistingShipment_UpdatesPickupWindowUsingClockTime()
    {
        var shipment = SomeShipment();
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);
        var clockTime = BookedAt.AddHours(1);
        FakeSimulationClock.SetUp(unitOfWork, clockTime);

        var handler = new UpdatePickupWindowHandler(unitOfWork.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));
        var newWindow = TimeWindow.Create(BookedAt.AddHours(3), BookedAt.AddHours(5));

        await handler.HandleAsync(new UpdatePickupWindowRequest(shipment.Id, newWindow));

        Assert.Same(newWindow, shipment.PickupWindow);
        Assert.Equal(clockTime.AddMinutes(30), shipment.OfferDeadline);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UnknownShipmentId_Throws_NeverSaves()
    {
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Shipment?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = new UpdatePickupWindowHandler(unitOfWork.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UpdatePickupWindowRequest(Guid.NewGuid(), TimeWindow.Create(BookedAt, BookedAt.AddHours(1)))));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShipmentAlreadyBooked_DomainRejectionPropagatesUnwrapped()
    {
        var shipment = SomeShipment();
        shipment.AssignToCompany(Guid.NewGuid());
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);
        FakeSimulationClock.SetUp(unitOfWork, BookedAt.AddHours(1));

        var handler = new UpdatePickupWindowHandler(unitOfWork.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UpdatePickupWindowRequest(shipment.Id, TimeWindow.Create(BookedAt.AddHours(3), BookedAt.AddHours(5)))));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
