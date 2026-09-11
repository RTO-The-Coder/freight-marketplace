using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetTruckPositionHandlerTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);
    private static readonly GeoLocation Office = GeoLocation.Create(50.11, 8.68);
    private static readonly GeoLocation Pickup = GeoLocation.Create(52.52, 13.405);
    private static readonly GeoLocation Delivery = GeoLocation.Create(48.1351, 11.582);

    private static Truck SomeTruck(Guid companyId)
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(companyId);
        return truck;
    }

    private static TruckingCompany SomeCompany(Guid companyId) =>
        TruckingCompany.Create(companyId, "Acme Trucking", Office);

    private static Trip OpenTripWithShipment(Guid truckId, Guid companyId)
    {
        var trip = Trip.Open(truckId, companyId, StartedAt);
        trip.AssignShipment(
            Guid.NewGuid(), Capacity.Create(100, 1), Pickup, Delivery, Office,
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            new LegPlan(new RouteSegment(20, 6), null, new RouteSegment(20, 6), null, new RouteSegment(20, 6)));
        return trip;
    }

    private static Mock<IUnitOfWork> SetUp(Truck? truck, TruckingCompany? company, Trip? trip)
    {
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        var companies = new Mock<ITruckingCompanyRepository>();
        companies.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(company);
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(trip);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task GetTruckPositionAsync_NoOpenTrip_ReturnsOfficePosition()
    {
        var companyId = Guid.NewGuid();
        var truck = SomeTruck(companyId);
        var company = SomeCompany(companyId);
        var unitOfWork = SetUp(truck, company, null);

        var handler = new GetTruckPositionHandler(unitOfWork.Object);
        var dto = await handler.GetTruckPositionAsync(new GetTruckPositionRequest(truck.Id));

        Assert.Equal(Office.Latitude, dto.Latitude);
        Assert.Equal(Office.Longitude, dto.Longitude);
        Assert.Null(dto.HeadingToStopId);
        Assert.Equal(0, dto.LegProgressFraction);
        Assert.Null(dto.TripId);
    }

    [Fact]
    public async Task GetTruckPositionAsync_OpenTripButCurrentProgressNull_ReturnsOfficePosition()
    {
        var companyId = Guid.NewGuid();
        var truck = SomeTruck(companyId);
        var company = SomeCompany(companyId);
        var trip = OpenTripWithShipment(truck.Id, companyId);
        // Truck's CurrentProgress was never synced - staged but not yet moving.
        var unitOfWork = SetUp(truck, company, trip);

        var handler = new GetTruckPositionHandler(unitOfWork.Object);
        var dto = await handler.GetTruckPositionAsync(new GetTruckPositionRequest(truck.Id));

        Assert.Equal(Office.Latitude, dto.Latitude);
        Assert.Equal(Office.Longitude, dto.Longitude);
        Assert.Equal(trip.Id, dto.TripId);
    }

    [Fact]
    public async Task GetTruckPositionAsync_MidLeg_ReturnsInterpolatedPositionAndFraction()
    {
        var companyId = Guid.NewGuid();
        var truck = SomeTruck(companyId);
        var company = SomeCompany(companyId);
        var trip = OpenTripWithShipment(truck.Id, companyId);
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        // Pickup leg is 6 ticks total - advance 3, i.e. exactly halfway.
        truck.CurrentProgress!.AdvanceByTicks(3);
        var unitOfWork = SetUp(truck, company, trip);

        var handler = new GetTruckPositionHandler(unitOfWork.Object);
        var dto = await handler.GetTruckPositionAsync(new GetTruckPositionRequest(truck.Id));

        var pickupStop = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var expected = Office.InterpolateTo(Pickup, 0.5);
        Assert.Equal(expected.Latitude, dto.Latitude, precision: 9);
        Assert.Equal(expected.Longitude, dto.Longitude, precision: 9);
        Assert.Equal(pickupStop.Id, dto.HeadingToStopId);
        Assert.Equal(0.5, dto.LegProgressFraction);
    }

    [Fact]
    public async Task GetTruckPositionAsync_PastFirstStop_InterpolatesFromLastReachedStopNotOffice()
    {
        var companyId = Guid.NewGuid();
        var truck = SomeTruck(companyId);
        var company = SomeCompany(companyId);
        var trip = OpenTripWithShipment(truck.Id, companyId);
        var pickupStop = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        trip.MarkStopReached(pickupStop.Id, StartedAt.AddMinutes(30));
        truck.SyncProgressToNextStop(trip, previousNextStopId: pickupStop.Id);
        truck.CurrentProgress!.AdvanceByTicks(3);
        var unitOfWork = SetUp(truck, company, trip);

        var handler = new GetTruckPositionHandler(unitOfWork.Object);
        var dto = await handler.GetTruckPositionAsync(new GetTruckPositionRequest(truck.Id));

        var expected = Pickup.InterpolateTo(Delivery, 0.5);
        Assert.Equal(expected.Latitude, dto.Latitude, precision: 9);
        Assert.Equal(expected.Longitude, dto.Longitude, precision: 9);
    }

    [Fact]
    public async Task GetTruckPositionAsync_TruckWithoutCompany_Throws()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        var unitOfWork = SetUp(truck, null, null);

        var handler = new GetTruckPositionHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GetTruckPositionAsync(new GetTruckPositionRequest(truck.Id)));
    }

    [Fact]
    public async Task GetTruckPositionAsync_UnknownTruckId_Throws()
    {
        var unitOfWork = SetUp(null, null, null);

        var handler = new GetTruckPositionHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GetTruckPositionAsync(new GetTruckPositionRequest(Guid.NewGuid())));
    }
}
