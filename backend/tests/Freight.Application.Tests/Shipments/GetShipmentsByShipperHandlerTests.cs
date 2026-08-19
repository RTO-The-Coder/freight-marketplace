using Freight.Application.Shipments;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Shipment;
using Freight.Domain.ValueObjects;
using Moq;
using ShipmentAggregate = Freight.Domain.Shipment.Shipment;

namespace Freight.Application.Tests.Shipments;

public sealed class GetShipmentsByShipperHandlerTests
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
    public async Task HandleAsync_ReturnsShipmentsForThatShipperOnly()
    {
        var shipments = new Mock<IShipmentRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var shipperId = Guid.NewGuid();
        var shipment = ShipmentAggregate.Book(
            shipperId, Pickup(), Delivery(), ValidLoad(), TruckType.Flatbed,
            ValidPickupWindow(), ValidDeliveryWindow(), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        shipments.Setup(s => s.GetByShipperIdAsync(shipperId, It.IsAny<CancellationToken>())).ReturnsAsync([shipment]);

        var handler = new GetShipmentsByShipperHandler(unitOfWork.Object);

        var response = await handler.HandleAsync(new GetShipmentsByShipperRequest(shipperId));

        var dto = Assert.Single(response.Shipments);
        Assert.Equal(shipment.Id, dto.ShipmentId);
        Assert.Equal(ShipmentStatus.Pending, dto.Status);
        Assert.Equal(shipment.PickupLocation.Latitude, dto.PickupLatitude);
        Assert.Equal(shipment.RequiredTruckType, dto.RequiredTruckType);
    }
}
