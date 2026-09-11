using Freight.Application.Client;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Common;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Client;

public sealed class BookShipmentHandlerTests
{
    private static BookShipmentRequest SomeRequest() => new(
        Guid.NewGuid(),
        GeoLocation.Create(52.52, 13.405),
        GeoLocation.Create(48.1351, 11.582),
        Capacity.Create(100, 5),
        TruckType.Refrigerated,
        TimeWindow.Create(new DateTime(2026, 1, 1, 9, 0, 0), new DateTime(2026, 1, 1, 11, 0, 0)),
        TimeWindow.Create(new DateTime(2026, 1, 1, 15, 0, 0), new DateTime(2026, 1, 1, 17, 0, 0)));

    [Fact]
    public async Task BookShipmentAsync_ExistingClock_StampsBookedAtFromClockCurrentTime()
    {
        var shipments = new Mock<IShipmentRepository>();
        Shipment? addedShipment = null;
        shipments.Setup(s => s.Add(It.IsAny<Shipment>())).Callback<Shipment>(s => addedShipment = s);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);
        var clockTime = new DateTime(2026, 1, 1, 6, 0, 0);
        FakeSimulationClock.SetUp(unitOfWork, clockTime);

        var handler = new BookShipmentHandler(unitOfWork.Object, new FakeTimeProvider(DateTimeOffset.UtcNow));
        var request = SomeRequest();

        var response = await handler.BookShipmentAsync(request);

        Assert.NotNull(addedShipment);
        Assert.Equal(clockTime.AddMinutes(30), addedShipment!.OfferDeadline);
        Assert.Equal(request.ShipperId, addedShipment.ShipperId);
        Assert.Same(request.PickupLocation, addedShipment.PickupLocation);
        Assert.Same(request.DeliveryLocation, addedShipment.DeliveryLocation);
        Assert.Same(request.Load, addedShipment.Load);
        Assert.Equal(request.RequiredTruckType, addedShipment.RequiredTruckType);
        Assert.Same(request.PickupWindow, addedShipment.PickupWindow);
        Assert.Same(request.DeliveryWindow, addedShipment.DeliveryWindow);
        Assert.Equal(response.ShipmentId, addedShipment.Id);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BookShipmentAsync_NoClockYet_SeedsClockFromTimeProviderThenUsesIt()
    {
        var shipments = new Mock<IShipmentRepository>();
        Shipment? addedShipment = null;
        shipments.Setup(s => s.Add(It.IsAny<Shipment>())).Callback<Shipment>(s => addedShipment = s);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var seedTimeOffset = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
        var simulationClockRepo = new Mock<Freight.Domain.Simulation.Abstractions.ISimulationClockRepository>();
        simulationClockRepo
            .Setup(r => r.GetOrCreateAsync(It.IsAny<Func<DateTime>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<DateTime> seed, CancellationToken _) => Freight.Domain.Simulation.SimulationClock.Create(seed()));
        unitOfWork.SetupGet(u => u.SimulationClock).Returns(simulationClockRepo.Object);

        var handler = new BookShipmentHandler(unitOfWork.Object, new FakeTimeProvider(seedTimeOffset));

        await handler.BookShipmentAsync(SomeRequest());

        Assert.NotNull(addedShipment);
        Assert.Equal(seedTimeOffset.UtcDateTime.AddMinutes(30), addedShipment!.OfferDeadline);
    }
}
