using Freight.Application.Client;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Common;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Client;

public sealed class GetShipmentsByShipperHandlerTests
{
    private static readonly DateTime BookedAt = new(2026, 1, 1, 8, 0, 0);

    private static Shipment SomeShipment(Guid shipperId) => Shipment.Book(
        Guid.NewGuid(), shipperId,
        GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
        Capacity.Create(100, 5), TruckType.Refrigerated,
        TimeWindow.Create(BookedAt.AddHours(1), BookedAt.AddHours(3)),
        TimeWindow.Create(BookedAt.AddHours(5), BookedAt.AddHours(7)),
        BookedAt);

    [Fact]
    public async Task GetShipmentsByShipperAsync_ForwardsRequestedShipperIdVerbatim()
    {
        var shipperId = Guid.NewGuid();
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByShipperIdAsync(shipperId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = new GetShipmentsByShipperHandler(unitOfWork.Object);
        await handler.GetShipmentsByShipperAsync(new GetShipmentsByShipperRequest(shipperId));

        shipments.Verify(s => s.GetByShipperIdAsync(shipperId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetShipmentsByShipperAsync_DoesNotFilterByStatus_ReturnsFullHistoryIncludingNonPending()
    {
        var shipperId = Guid.NewGuid();
        var shipment = SomeShipment(shipperId);
        shipment.AssignToCompany(Guid.NewGuid());
        shipment.MarkPickedUp(BookedAt.AddHours(2));
        shipment.MarkDelivered(BookedAt.AddHours(6));

        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByShipperIdAsync(shipperId, It.IsAny<CancellationToken>())).ReturnsAsync([shipment]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = new GetShipmentsByShipperHandler(unitOfWork.Object);
        var response = await handler.GetShipmentsByShipperAsync(new GetShipmentsByShipperRequest(shipperId));

        var dto = Assert.Single(response.Shipments);
        Assert.Equal(Freight.Domain.Client.Enums.ShipmentStatus.Delivered, dto.Status);
    }

    [Fact]
    public async Task GetShipmentsByShipperAsync_NoShipments_ReturnsEmptyNotNullList()
    {
        var shipperId = Guid.NewGuid();
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByShipperIdAsync(shipperId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = new GetShipmentsByShipperHandler(unitOfWork.Object);
        var response = await handler.GetShipmentsByShipperAsync(new GetShipmentsByShipperRequest(shipperId));

        Assert.NotNull(response.Shipments);
        Assert.Empty(response.Shipments);
    }
}
