using Freight.Application.Shipments;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Shipment;
using Freight.Domain.ValueObjects;
using Moq;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;

namespace Freight.Application.Tests.Shipments;

public sealed class UpdatePickupWindowHandlerTests
{
    private static GeoLocation Pickup() => GeoLocation.Create(52.5200, 13.4050);
    private static GeoLocation Delivery() => GeoLocation.Create(48.1351, 11.5820);
    private static Capacity ValidLoad() => Capacity.Create(500, 5);
    private static TimeWindow ValidPickupWindow() => TimeWindow.Create(
        new DateTime(2026, 1, 2, 8, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));
    private static TimeWindow ValidDeliveryWindow() => TimeWindow.Create(
        new DateTime(2026, 1, 2, 14, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 1, 2, 18, 0, 0, DateTimeKind.Utc));

    private static ShipmentAggregate NewPendingShipment(DateTime bookedAt) => ShipmentAggregate.Book(
        Guid.NewGuid(), Pickup(), Delivery(), ValidLoad(), TruckType.Flatbed, ValidPickupWindow(), ValidDeliveryWindow(), bookedAt);

    [Fact]
    public async Task HandleAsync_PendingShipment_UpdatesWindowAndSaves()
    {
        var shipments = new Mock<IShipmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var bookedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var shipment = NewPendingShipment(bookedAt);
        shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

        var updatedAt = new DateTime(2026, 1, 1, 6, 0, 0, DateTimeKind.Utc);
        var timeProvider = new FakeTimeProvider(updatedAt);
        var handler = new UpdatePickupWindowHandler(unitOfWork.Object, timeProvider);
        var newWindow = TimeWindow.Create(
            new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));

        await handler.HandleAsync(new UpdatePickupWindowRequest(shipment.Id, newWindow));

        Assert.Equal(newWindow, shipment.PickupWindow);
        Assert.Equal(updatedAt.AddMinutes(30), shipment.OfferDeadline);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UnknownShipmentId_Throws()
    {
        var shipments = new Mock<IShipmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);
        shipments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((ShipmentAggregate?)null);

        var handler = new UpdatePickupWindowHandler(unitOfWork.Object, new FakeTimeProvider(DateTime.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UpdatePickupWindowRequest(Guid.NewGuid(), ValidPickupWindow())));
    }

    [Fact]
    public async Task HandleAsync_NonPendingShipment_ThrowsAndDoesNotSave()
    {
        var shipments = new Mock<IShipmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var shipment = NewPendingShipment(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        shipment.AssignToCompany(Guid.NewGuid());
        shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

        var handler = new UpdatePickupWindowHandler(unitOfWork.Object, new FakeTimeProvider(DateTime.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new UpdatePickupWindowRequest(shipment.Id, ValidPickupWindow())));

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
