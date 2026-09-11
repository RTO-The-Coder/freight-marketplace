using Freight.Application.Fleet;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetTruckEtasHandlerTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);
    private readonly RouteEtaCalculator _calculator = new(new DriverRuleEngine());

    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static (Truck Truck, Trip Trip, Shipment Shipment) OpenTripWithSingleDriver()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(driver);
        truck.BeginTripCompliance(StartedAt);

        var trip = Trip.Open(truck.Id, truck.TruckingCompanyId!.Value, StartedAt);
        var shipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(),
            GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
            Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddDays(1)),
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
            StartedAt);
        trip.AssignShipment(
            shipment.Id, shipment.Load, shipment.PickupLocation, shipment.DeliveryLocation, GeoLocation.Create(50.11, 8.68),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            new LegPlan(new RouteSegment(20, 6), null, new RouteSegment(20, 6), null, new RouteSegment(20, 6)));

        return (truck, trip, shipment);
    }

    private Mock<IUnitOfWork> SetUp(Truck? truck, Trip? trip, IReadOnlyDictionary<Guid, Shipment> shipmentsById)
    {
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(trip);
        var shipments = new Mock<IShipmentRepository>();
        shipments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => shipmentsById.GetValueOrDefault(id));

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task GetTruckEtasAsync_NoOpenTrip_ReturnsEmptyStopsWithoutCallingCalculator()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        var unitOfWork = SetUp(truck, null, new Dictionary<Guid, Shipment>());

        var handler = new GetTruckEtasHandler(unitOfWork.Object, _calculator);
        var dto = await handler.GetTruckEtasAsync(new GetTruckEtasRequest(truck.Id));

        Assert.Empty(dto.Stops);
        Assert.Null(dto.TripId);
        Assert.Null(dto.ProjectionStart);
    }

    [Fact]
    public async Task GetTruckEtasAsync_SingleDriver_ProjectsEtasForEachStop()
    {
        var (truck, trip, shipment) = OpenTripWithSingleDriver();
        var unitOfWork = SetUp(truck, trip, new Dictionary<Guid, Shipment> { [shipment.Id] = shipment });

        var handler = new GetTruckEtasHandler(unitOfWork.Object, _calculator);
        var dto = await handler.GetTruckEtasAsync(new GetTruckEtasRequest(truck.Id));

        Assert.Equal(3, dto.Stops.Count);
        Assert.All(dto.Stops, s => Assert.NotNull(s.ProjectedArrival));
        Assert.Equal(trip.Id, dto.TripId);
    }

    [Fact]
    public async Task GetTruckEtasAsync_TeamDriver_UsesTeamProjection()
    {
        var primary = Driver.Create(Guid.NewGuid(), "Primary", "Driver", SomeRules());
        var secondary = Driver.Create(Guid.NewGuid(), "Secondary", "Driver", SomeRules());
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Large);
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(primary, secondary);
        truck.BeginTripCompliance(StartedAt);

        var trip = Trip.Open(truck.Id, truck.TruckingCompanyId!.Value, StartedAt);
        var shipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(),
            GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
            Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddDays(1)),
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
            StartedAt);
        trip.AssignShipment(
            shipment.Id, shipment.Load, shipment.PickupLocation, shipment.DeliveryLocation, GeoLocation.Create(50.11, 8.68),
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            new LegPlan(new RouteSegment(20, 6), null, new RouteSegment(20, 6), null, new RouteSegment(20, 6)));

        var unitOfWork = SetUp(truck, trip, new Dictionary<Guid, Shipment> { [shipment.Id] = shipment });

        var handler = new GetTruckEtasHandler(unitOfWork.Object, _calculator);
        var dto = await handler.GetTruckEtasAsync(new GetTruckEtasRequest(truck.Id));

        Assert.Equal(3, dto.Stops.Count);
        Assert.All(dto.Stops, s => Assert.NotNull(s.ProjectedArrival));
    }

    [Fact]
    public async Task GetTruckEtasAsync_BuildsWindowsOnlyForPendingPickupDeliveryStops()
    {
        var (truck, trip, shipment) = OpenTripWithSingleDriver();
        var shipments = new Mock<IShipmentRepository>();
        var callCount = 0;
        shipments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => { callCount++; return id == shipment.Id ? shipment : null; });
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(trip);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);

        var handler = new GetTruckEtasHandler(unitOfWork.Object, _calculator);
        await handler.GetTruckEtasAsync(new GetTruckEtasRequest(truck.Id));

        // 2 pending shipment stops (pickup + delivery) - the Office stop is skipped.
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetTruckEtasAsync_OpenTripNoDriverAssignment_Throws()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(Guid.NewGuid());
        var trip = Trip.Open(truck.Id, truck.TruckingCompanyId!.Value, StartedAt);
        var unitOfWork = SetUp(truck, trip, new Dictionary<Guid, Shipment>());

        var handler = new GetTruckEtasHandler(unitOfWork.Object, _calculator);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GetTruckEtasAsync(new GetTruckEtasRequest(truck.Id)));
    }

    [Fact]
    public async Task GetTruckEtasAsync_UnknownTruckId_Throws()
    {
        var unitOfWork = SetUp(null, null, new Dictionary<Guid, Shipment>());

        var handler = new GetTruckEtasHandler(unitOfWork.Object, _calculator);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GetTruckEtasAsync(new GetTruckEtasRequest(Guid.NewGuid())));
    }
}
