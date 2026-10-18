using Freight.Application.Client;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Client;

public sealed class GetShipmentsByShipperHandlerTests
{
    private static readonly DateTime BookedAt = new(2026, 1, 1, 8, 0, 0);

    private static Shipment SomeShipment(Guid shipperId) => Shipment.Book(
        Guid.NewGuid(), shipperId,
        GeoLocation.Create(52.52, 13.405),
        "Pickup place", GeoLocation.Create(48.1351, 11.582), "Delivery place",
        Capacity.Create(100, 5), TruckType.Refrigerated,
        TimeWindow.Create(BookedAt.AddHours(1), BookedAt.AddHours(3)),
        TimeWindow.Create(BookedAt.AddHours(5), BookedAt.AddHours(7)),
        BookedAt);

    private static GetShipmentsByShipperHandler NewHandler(
        Mock<IUnitOfWork> unitOfWork, IReadOnlyList<ShipmentOffer>? offers = null, DateTime? now = null)
    {
        FakeSimulationClock.SetUp(unitOfWork, now ?? BookedAt);
        var offerRepo = new Mock<IShipmentOfferRepository>();
        offerRepo.Setup(r => r.GetByShipmentIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(offers ?? []);
        unitOfWork.SetupGet(u => u.ShipmentOffers).Returns(offerRepo.Object);
        return new GetShipmentsByShipperHandler(unitOfWork.Object, TimeProvider.System);
    }

    [Fact]
    public async Task GetShipmentsByShipperAsync_CountsOnlyWaitingOffers()
    {
        var shipperId = Guid.NewGuid();
        var shipment = SomeShipment(shipperId);
        var waiting = ShipmentOffer.Create(shipment.Id, Guid.NewGuid(), Guid.NewGuid(), 0, 1, 5, 500m, null, BookedAt);
        var limitPassed = ShipmentOffer.Create(shipment.Id, Guid.NewGuid(), Guid.NewGuid(), 0, 1, 5, 600m, BookedAt.AddMinutes(10), BookedAt);
        var rejected = ShipmentOffer.Create(shipment.Id, Guid.NewGuid(), Guid.NewGuid(), 0, 1, 5, 700m, null, BookedAt);
        rejected.Reject();

        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByShipperIdAsync(shipperId, It.IsAny<CancellationToken>())).ReturnsAsync([shipment]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = NewHandler(unitOfWork, [waiting, limitPassed, rejected], BookedAt.AddMinutes(30));
        var response = await handler.GetShipmentsByShipperAsync(new GetShipmentsByShipperRequest(shipperId));

        var dto = Assert.Single(response.Shipments);
        Assert.Equal(1, dto.WaitingOfferCount);
        Assert.True(dto.OffersOpen);
    }

    [Fact]
    public async Task GetShipmentsByShipperAsync_ForwardsRequestedShipperIdVerbatim()
    {
        var shipperId = Guid.NewGuid();
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByShipperIdAsync(shipperId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = NewHandler(unitOfWork);
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

        var handler = NewHandler(unitOfWork);
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

        var handler = NewHandler(unitOfWork);
        var response = await handler.GetShipmentsByShipperAsync(new GetShipmentsByShipperRequest(shipperId));

        Assert.NotNull(response.Shipments);
        Assert.Empty(response.Shipments);
    }

    // --- Where each shipment is now ------------------------------------------------

    private static readonly GeoLocation Pickup = GeoLocation.Create(52.52, 13.405);
    private static readonly GeoLocation Delivery = GeoLocation.Create(48.1351, 11.582);
    private static readonly GeoLocation Office = GeoLocation.Create(50.11, 8.68);

    private static async Task<IReadOnlyList<ShipmentSummaryDto>> ListAsync(
        Guid shipperId, IReadOnlyList<Shipment> shipments, Trip? openTrip = null, Truck? truck = null, TruckingCompany? company = null)
    {
        var shipmentRepository = new Mock<IShipmentRepository>();
        shipmentRepository.Setup(s => s.GetByShipperIdAsync(shipperId, It.IsAny<CancellationToken>())).ReturnsAsync(shipments);
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(openTrip is null ? [] : [openTrip]);
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        var companies = new Mock<ITruckingCompanyRepository>();
        companies.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipmentRepository.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);

        var response = await NewHandler(unitOfWork).GetShipmentsByShipperAsync(new GetShipmentsByShipperRequest(shipperId));
        return response.Shipments;
    }

    [Fact]
    public async Task GetShipmentsByShipperAsync_PendingShipment_IsAtItsPickupAndReadsNoTrips()
    {
        var shipperId = Guid.NewGuid();
        var shipment = SomeShipment(shipperId);

        var dto = Assert.Single(await ListAsync(shipperId, [shipment]));

        Assert.Equal(Pickup.Latitude, dto.CurrentLatitude);
        Assert.Equal(Pickup.Longitude, dto.CurrentLongitude);
        Assert.Equal("Pickup place", dto.PickupPlaceName);
        Assert.Equal("Delivery place", dto.DeliveryPlaceName);
    }

    [Fact]
    public async Task GetShipmentsByShipperAsync_DeliveredShipment_IsAtItsDelivery()
    {
        var shipperId = Guid.NewGuid();
        var shipment = SomeShipment(shipperId);
        shipment.AssignToCompany(Guid.NewGuid());
        shipment.MarkPickedUp(BookedAt.AddHours(2));
        shipment.MarkDelivered(BookedAt.AddHours(6));

        var dto = Assert.Single(await ListAsync(shipperId, [shipment]));

        Assert.Equal(Delivery.Latitude, dto.CurrentLatitude);
        Assert.Equal(Delivery.Longitude, dto.CurrentLongitude);
    }

    [Fact]
    public async Task GetShipmentsByShipperAsync_InTransitShipment_IsWhereItsTruckIs()
    {
        var shipperId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var shipment = SomeShipment(shipperId);
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(companyId);
        var company = TruckingCompany.Create(companyId, "Acme Trucking", Office, "Office place");
        var trip = Trip.Open(truck.Id, companyId, BookedAt);
        trip.AssignShipment(
            shipment.Id, Capacity.Create(100, 5), Pickup, Delivery, Office,
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            new LegPlan(new RouteSegment(20, 6), null, new RouteSegment(20, 6), null, new RouteSegment(20, 6)));
        shipment.AssignToCompany(companyId);

        // Picked up; the truck is now halfway (3 of 6 ticks) from the pickup to the delivery.
        var pickupStop = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        trip.MarkStopReached(pickupStop.Id, BookedAt.AddHours(2));
        shipment.MarkPickedUp(BookedAt.AddHours(2));
        truck.SyncProgressToNextStop(trip, previousNextStopId: pickupStop.Id);
        truck.CurrentProgress!.AdvanceByTicks(3);

        var dto = Assert.Single(await ListAsync(shipperId, [shipment], trip, truck, company));

        var expected = Pickup.InterpolateTo(Delivery, 0.5);
        Assert.Equal(expected.Latitude, dto.CurrentLatitude!.Value, precision: 9);
        Assert.Equal(expected.Longitude, dto.CurrentLongitude!.Value, precision: 9);
    }
}
