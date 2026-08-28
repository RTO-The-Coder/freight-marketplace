using Freight.Application.Shipments;
using Freight.Application.Tests;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Client;
using Freight.Domain.ValueObjects;
using Moq;
using ShipmentAggregate = Freight.Domain.Client.Shipment;

namespace Freight.Application.Tests.Shipments;

public sealed class BookShipmentHandlerTests
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

    [Fact]
    public async Task HandleAsync_ValidRequest_BooksPendingShipmentAndSaves()
    {
        var shipments = new Mock<IShipmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        ShipmentAggregate? added = null;
        shipments.Setup(s => s.Add(It.IsAny<ShipmentAggregate>())).Callback<ShipmentAggregate>(s => added = s);

        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        FakeSimulationClock.SetUp(unitOfWork, now);
        var timeProvider = new FakeTimeProvider(now);
        var handler = new BookShipmentHandler(unitOfWork.Object, timeProvider);
        var shipperId = Guid.NewGuid();

        var response = await handler.HandleAsync(new BookShipmentRequest(
            shipperId, Pickup(), Delivery(), ValidLoad(), TruckType.Flatbed, ValidPickupWindow(), ValidDeliveryWindow()));

        Assert.NotNull(added);
        Assert.Equal(response.ShipmentId, added!.Id);
        Assert.Equal(shipperId, added.ShipperId);
        Assert.Null(added.TruckingCompanyId);
        Assert.Equal(ShipmentStatus.Pending, added.Status);
        Assert.Equal(now.AddMinutes(30), added.OfferDeadline);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
