using Freight.Application.Fleet;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetTruckDetailHandlerTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);

    private static Truck SomeTruck() =>
        Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);

    private static Mock<IUnitOfWork> SetUp(
        Truck? truck, Trip? trip, IReadOnlyList<Shipment>? shipments = null, TruckingCompany? company = null)
    {
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(trip);

        // Stop place names are read from the trip's shipments and the office's company.
        var shipmentRepository = new Mock<IShipmentRepository>();
        shipmentRepository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipments ?? []);
        var companies = new Mock<ITruckingCompanyRepository>();
        companies.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipmentRepository.Object);
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task GetTruckDetailAsync_NoOpenTrip_ReturnsEmptyStopsNotNull()
    {
        var truck = SomeTruck();
        var unitOfWork = SetUp(truck, null);

        var handler = new GetTruckDetailHandler(unitOfWork.Object);
        var dto = await handler.GetTruckDetailAsync(new GetTruckDetailRequest(truck.Id));

        Assert.NotNull(dto.Stops);
        Assert.Empty(dto.Stops);
        Assert.Null(dto.DriverConfigurationType);
        Assert.Null(dto.PrimaryDriver);
    }

    [Fact]
    public async Task GetTruckDetailAsync_OpenTripWithReachedAndPendingStops_MapsEveryStopField()
    {
        var truck = SomeTruck();
        var company = TruckingCompany.Create(
            Guid.NewGuid(), "Acme", GeoLocation.Create(50.11, 8.68), "60594 Sachsenhausen, Frankfurt");
        var shipment = Shipment.Book(
            Guid.NewGuid(), GeoLocation.Create(52.52, 13.405), "Warehouse — 10178 Mitte, Berlin",
            GeoLocation.Create(48.1351, 11.582), "Depot — 80803 Schwabing, Munich", Capacity.Create(100, 1),
            TruckType.Refrigerated, TimeWindow.Create(StartedAt, StartedAt.AddHours(4)),
            TimeWindow.Create(StartedAt.AddHours(6), StartedAt.AddHours(10)), StartedAt);
        var trip = Trip.Open(truck.Id, company.Id, StartedAt);
        trip.AssignShipment(
            shipment.Id, Capacity.Create(100, 1),
            GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582), GeoLocation.Create(50.11, 8.68),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            new LegPlan(new RouteSegment(20, 6), null, new RouteSegment(20, 6), null, new RouteSegment(20, 6)));
        var pickup = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var reachedAt = StartedAt.AddHours(1);
        trip.MarkStopReached(pickup.Id, reachedAt);
        var unitOfWork = SetUp(truck, trip, [shipment], company);

        var handler = new GetTruckDetailHandler(unitOfWork.Object);
        var dto = await handler.GetTruckDetailAsync(new GetTruckDetailRequest(truck.Id));

        Assert.Equal(3, dto.Stops.Count);
        var reachedDto = dto.Stops.First(s => s.StopId == pickup.Id);
        Assert.Equal(pickup.ShipmentId, reachedDto.ShipmentId);
        Assert.Equal(StopKind.Pickup, reachedDto.Kind);
        Assert.Equal(StopStatus.Reached, reachedDto.Status);
        Assert.Equal(pickup.Sequence, reachedDto.Sequence);
        Assert.Equal(pickup.Location.Latitude, reachedDto.Latitude);
        Assert.Equal(pickup.Location.Longitude, reachedDto.Longitude);
        Assert.Equal("Warehouse — 10178 Mitte, Berlin", reachedDto.PlaceName);
        Assert.Equal(pickup.IncomingLegDistanceKm, reachedDto.IncomingLegDistanceKm);
        Assert.Equal(pickup.IncomingLegTimeTick, reachedDto.IncomingLegTimeTick);
        Assert.Equal(reachedAt, reachedDto.ReachedAt);

        var pendingDto = dto.Stops.First(s => s.Kind == StopKind.Delivery);
        Assert.Equal(StopStatus.Pending, pendingDto.Status);
        Assert.Null(pendingDto.ReachedAt);
        Assert.Equal("Depot — 80803 Schwabing, Munich", pendingDto.PlaceName);

        // The office stop shows the company's office place name.
        Assert.Equal("60594 Sachsenhausen, Frankfurt", dto.Stops.Single(s => s.Kind == StopKind.Office).PlaceName);
    }

    [Fact]
    public async Task GetTruckDetailAsync_SingleDriver_MapsPrimaryOnly()
    {
        var truck = SomeTruck();
        var companyId = Guid.NewGuid();
        truck.AssignToCompany(companyId);
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe",
            Freight.Domain.ValueObjects.DrivingRules.Create(
                Freight.Domain.ValueObjects.RuleVariants.DrivingBreakRule.FullBreak,
                Freight.Domain.ValueObjects.RuleVariants.DailyRestRule.FullRest,
                Freight.Domain.ValueObjects.RuleVariants.WeeklyRestRule.FullWeeklyRest, false),
            companyId);
        truck.AssignDrivers(driver);
        var unitOfWork = SetUp(truck, null);

        var handler = new GetTruckDetailHandler(unitOfWork.Object);
        var dto = await handler.GetTruckDetailAsync(new GetTruckDetailRequest(truck.Id));

        Assert.Equal(DriverConfigurationType.Single, dto.DriverConfigurationType);
        Assert.Equal(driver.Id, dto.PrimaryDriver!.DriverId);
        Assert.Null(dto.SecondaryDriver);
    }

    [Fact]
    public async Task GetTruckDetailAsync_UnknownTruckId_Throws()
    {
        var unitOfWork = SetUp(null, null);

        var handler = new GetTruckDetailHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GetTruckDetailAsync(new GetTruckDetailRequest(Guid.NewGuid())));
    }
}
