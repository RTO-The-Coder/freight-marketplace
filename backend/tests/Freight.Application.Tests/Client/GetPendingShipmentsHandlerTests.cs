using Freight.Application.Client;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Client;

public sealed class GetPendingShipmentsHandlerTests
{
    private static readonly DateTime BookedAt = new(2026, 1, 1, 8, 0, 0);

    private static Shipment SomeShipment() => Shipment.Book(
        Guid.NewGuid(), Guid.NewGuid(),
        GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
        Capacity.Create(100, 5), TruckType.Refrigerated,
        TimeWindow.Create(BookedAt.AddHours(1), BookedAt.AddHours(3)),
        TimeWindow.Create(BookedAt.AddHours(5), BookedAt.AddHours(7)),
        BookedAt);

    [Fact]
    public async Task GetPendingShipmentsAsync_QueriesByPendingStatusSpecifically()
    {
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByStatusAsync(ShipmentStatus.Pending, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = new GetPendingShipmentsHandler(unitOfWork.Object);
        await handler.GetPendingShipmentsAsync();

        shipments.Verify(s => s.GetByStatusAsync(ShipmentStatus.Pending, It.IsAny<CancellationToken>()), Times.Once);
        shipments.Verify(s => s.GetByStatusAsync(It.Is<ShipmentStatus>(status => status != ShipmentStatus.Pending), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetPendingShipmentsAsync_MapsEveryDtoFieldFromTheShipment()
    {
        var shipment = SomeShipment();
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByStatusAsync(ShipmentStatus.Pending, It.IsAny<CancellationToken>())).ReturnsAsync([shipment]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = new GetPendingShipmentsHandler(unitOfWork.Object);
        var response = await handler.GetPendingShipmentsAsync();

        var dto = Assert.Single(response.Shipments);
        Assert.Equal(shipment.Id, dto.ShipmentId);
        Assert.Null(dto.TruckingCompanyId);
        Assert.Equal(shipment.PickupLocation.Latitude, dto.PickupLatitude);
        Assert.Equal(shipment.PickupLocation.Longitude, dto.PickupLongitude);
        Assert.Equal(shipment.DeliveryLocation.Latitude, dto.DeliveryLatitude);
        Assert.Equal(shipment.DeliveryLocation.Longitude, dto.DeliveryLongitude);
        Assert.Equal(shipment.Load.WeightKg, dto.LoadWeightKg);
        Assert.Equal(shipment.Load.VolumeCubicMeters, dto.LoadVolumeCubicMeters);
        Assert.Equal(shipment.RequiredTruckType, dto.RequiredTruckType);
        Assert.Equal(shipment.PickupWindow.Earliest, dto.PickupWindowEarliest);
        Assert.Equal(shipment.PickupWindow.Latest, dto.PickupWindowLatest);
        Assert.Equal(shipment.DeliveryWindow.Earliest, dto.DeliveryWindowEarliest);
        Assert.Equal(shipment.DeliveryWindow.Latest, dto.DeliveryWindowLatest);
        Assert.Equal(shipment.OfferDeadline, dto.OfferDeadline);
        Assert.Equal(ShipmentStatus.Pending, dto.Status);
    }

    [Fact]
    public async Task GetPendingShipmentsAsync_NonePending_ReturnsEmptyNotNullList()
    {
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByStatusAsync(ShipmentStatus.Pending, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = new GetPendingShipmentsHandler(unitOfWork.Object);
        var response = await handler.GetPendingShipmentsAsync();

        Assert.NotNull(response.Shipments);
        Assert.Empty(response.Shipments);
    }
}
