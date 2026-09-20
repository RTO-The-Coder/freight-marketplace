using Freight.Application.Evaluation;
using Freight.Application.Fleet;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Evaluation;

public sealed class ShipmentEvaluationEngineTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);
    private static readonly GeoLocation Office = GeoLocation.Create(50.11, 8.68);
    private static readonly GeoLocation Pickup = GeoLocation.Create(52.52, 13.405);
    private static readonly GeoLocation Delivery = GeoLocation.Create(48.1351, 11.582);

    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static Shipment SomeShipment(
        TruckType requiredType = TruckType.Refrigerated,
        DateTime? pickupEarliest = null,
        DateTime? pickupLatest = null) => Shipment.Book(
        Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery,
        Capacity.Create(100, 1), requiredType,
        TimeWindow.Create(pickupEarliest ?? StartedAt, pickupLatest ?? StartedAt.AddDays(1)),
        TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
        StartedAt);

    private static Truck ReadyTruck(
        Guid companyId, TruckType type = TruckType.Refrigerated, TruckSize size = TruckSize.Medium, bool active = true)
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", type, size);
        truck.AssignToCompany(companyId);
        if (active)
        {
            truck.AssignDrivers(Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules()));
            truck.Activate();
        }

        return truck;
    }

    private sealed class Harness
    {
        public Mock<ITruckRepository> Trucks { get; } = new();
        public Mock<IShipmentRepository> Shipments { get; } = new();
        public Mock<ITripRepository> Trips { get; } = new();
        public Mock<ITruckingCompanyRepository> Companies { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public FakeRoutingService RoutingService { get; } = new();
        public Dictionary<Guid, Trip?> OpenTripByTruckId { get; } = [];
        public Dictionary<Guid, TruckingCompany> CompaniesById { get; } = [];

        public Harness(Guid companyId, IReadOnlyList<Truck> companyTrucks, Shipment shipment)
        {
            Trucks.Setup(t => t.GetByTruckingCompanyIdAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync(companyTrucks);
            Trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => companyTrucks.FirstOrDefault(t => t.Id == id));

            Shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);

            Trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid truckId, CancellationToken _) => OpenTripByTruckId.GetValueOrDefault(truckId));

            Companies.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => CompaniesById.GetValueOrDefault(id));

            UnitOfWork.SetupGet(u => u.Trucks).Returns(Trucks.Object);
            UnitOfWork.SetupGet(u => u.Shipments).Returns(Shipments.Object);
            UnitOfWork.SetupGet(u => u.Trips).Returns(Trips.Object);
            UnitOfWork.SetupGet(u => u.TruckingCompanies).Returns(Companies.Object);
            FakeSimulationClock.SetUp(UnitOfWork, StartedAt);
        }

        public void RegisterCompany(TruckingCompany company) => CompaniesById[company.Id] = company;

        public ShipmentEvaluationEngine NewEngine()
        {
            var routeEtaCalculator = new RouteEtaCalculator(new DriverRuleEngine());
            var planner = new ShipmentInsertionPlanner(
                UnitOfWork.Object,
                new ShipmentInsertionEvaluator(routeEtaCalculator),
                RoutingService,
                new FakeTimeProvider(StartedAt));
            return new ShipmentEvaluationEngine(UnitOfWork.Object, planner, routeEtaCalculator, new FakeTimeProvider(StartedAt));
        }
    }

    private static Harness NewHarnessForOneTruck(Truck truck, Shipment shipment)
    {
        var harness = new Harness(truck.TruckingCompanyId!.Value, [truck], shipment);
        harness.RegisterCompany(TruckingCompany.Create(truck.TruckingCompanyId!.Value, "Acme Trucking", Office));
        return harness;
    }

    // --- Pre-filter rejections ---

    [Fact]
    public async Task EvaluateForCompanyAsync_WrongTruckType_ReturnsNotFeasible()
    {
        var companyId = Guid.NewGuid();
        var truck = ReadyTruck(companyId, type: TruckType.Flatbed);
        var shipment = SomeShipment(requiredType: TruckType.Refrigerated);
        var harness = NewHarnessForOneTruck(truck, shipment);

        var results = await harness.NewEngine().EvaluateForCompanyAsync(shipment.Id, companyId);

        var result = Assert.Single(results);
        Assert.Equal(truck.Id, result.TruckId);
        Assert.False(result.IsFeasible);
        Assert.Empty(harness.RoutingService.Requests); // rejected before any OSRM call
    }

    [Fact]
    public async Task EvaluateForCompanyAsync_InactiveTruck_ReturnsNotFeasible()
    {
        var companyId = Guid.NewGuid();
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(companyId);
        truck.AssignDrivers(Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules()));
        // Never activated - IsActive stays false.
        var shipment = SomeShipment();
        var harness = NewHarnessForOneTruck(truck, shipment);

        var results = await harness.NewEngine().EvaluateForCompanyAsync(shipment.Id, companyId);

        Assert.False(Assert.Single(results).IsFeasible);
        Assert.Empty(harness.RoutingService.Requests);
    }

    [Fact]
    public async Task EvaluateForCompanyAsync_NoDriverAssigned_ReturnsNotFeasible()
    {
        var companyId = Guid.NewGuid();
        var truck = ReadyTruck(companyId, active: false);
        var shipment = SomeShipment();
        var harness = NewHarnessForOneTruck(truck, shipment);

        var results = await harness.NewEngine().EvaluateForCompanyAsync(shipment.Id, companyId);

        Assert.False(Assert.Single(results).IsFeasible);
        Assert.Empty(harness.RoutingService.Requests);
    }

    [Fact]
    public async Task EvaluateForCompanyAsync_ShipmentTooHeavyForTruck_ReturnsNotFeasible()
    {
        // No pre-filter shortcut for capacity (see PassesPreFilter's doc comment) - an
        // over-capacity shipment is still rejected, but via the real evaluator, which DOES
        // spend OSRM calls measuring the attempted position before rejecting it.
        var companyId = Guid.NewGuid();
        var truck = ReadyTruck(companyId, size: TruckSize.Small); // 2,800 kg / 20 m3
        var oversizedShipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery,
            Capacity.Create(5_000, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddDays(1)),
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
            StartedAt);
        var harness = NewHarnessForOneTruck(truck, oversizedShipment);

        var results = await harness.NewEngine().EvaluateForCompanyAsync(oversizedShipment.Id, companyId);

        Assert.False(Assert.Single(results).IsFeasible);
    }

    // --- No open trip: fresh truck ---

    [Fact]
    public async Task EvaluateForCompanyAsync_NoOpenTrip_FeasibleShipment_ReturnsFeasibleAtPositionZero()
    {
        var companyId = Guid.NewGuid();
        var truck = ReadyTruck(companyId);
        var shipment = SomeShipment();
        var harness = NewHarnessForOneTruck(truck, shipment);

        var results = await harness.NewEngine().EvaluateForCompanyAsync(shipment.Id, companyId);

        var result = Assert.Single(results);
        Assert.True(result.IsFeasible);
        Assert.Equal(0, result.PickupInsertIndex);
        Assert.Equal(0, result.DeliveryInsertIndex);
        Assert.NotNull(result.AddedDistanceKm);
        Assert.NotNull(result.AddedTimeTick);
        Assert.True(result.AddedDistanceKm > 0);
    }

    [Fact]
    public async Task EvaluateForCompanyAsync_NoOpenTrip_WindowTooTight_ReturnsNotFeasible()
    {
        var companyId = Guid.NewGuid();
        var truck = ReadyTruck(companyId);
        // Pickup window closes before the truck could possibly arrive (real leg time > 1 minute).
        var tightShipment = SomeShipment(pickupEarliest: StartedAt, pickupLatest: StartedAt.AddMinutes(1));
        var harness = NewHarnessForOneTruck(truck, tightShipment);

        var results = await harness.NewEngine().EvaluateForCompanyAsync(tightShipment.Id, companyId);

        Assert.False(Assert.Single(results).IsFeasible);
    }

    // --- Existing open trip: append after existing stops ---

    [Fact]
    public async Task EvaluateForCompanyAsync_ExistingTripWithRoom_FindsFeasiblePositionAfterExistingStops()
    {
        var companyId = Guid.NewGuid();
        var truck = ReadyTruck(companyId);
        var harness = NewHarnessForOneTruck(truck, SomeShipment());

        // Build an existing trip with one earlier shipment already on the route, far enough
        // ahead in time that the new shipment's pickup window opens well after it.
        var earlyShipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery,
            Capacity.Create(50, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddHours(1)),
            TimeWindow.Create(StartedAt, StartedAt.AddHours(2)),
            StartedAt);
        harness.Shipments.Setup(s => s.GetByIdAsync(earlyShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(earlyShipment);

        var trip = Trip.Open(truck.Id, companyId, StartedAt);
        var legPlan = new Freight.Domain.Fleet.ValueObjects.LegPlan(
            new Freight.Domain.Fleet.ValueObjects.RouteSegment(20, 6),
            null,
            new Freight.Domain.Fleet.ValueObjects.RouteSegment(20, 6),
            null,
            new Freight.Domain.Fleet.ValueObjects.RouteSegment(20, 6));
        trip.AssignShipment(earlyShipment.Id, earlyShipment.Load, Pickup, Delivery, Office, 0, 0, legPlan);
        truck.BeginTripCompliance(trip.StartedAt);
        harness.OpenTripByTruckId[truck.Id] = trip;

        var newShipment = SomeShipment(pickupEarliest: StartedAt, pickupLatest: StartedAt.AddDays(1));
        harness.Shipments.Setup(s => s.GetByIdAsync(newShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(newShipment);

        var results = await harness.NewEngine().EvaluateForCompanyAsync(newShipment.Id, companyId);

        var result = Assert.Single(results);
        Assert.True(result.IsFeasible);
        Assert.NotNull(result.AddedDistanceKm);
        Assert.True(result.AddedDistanceKm > 0); // adding a second shipment's legs costs something
    }

    // --- Multiple trucks: every truck evaluated, no early exit ---

    [Fact]
    public async Task EvaluateForCompanyAsync_TwoTrucksSameCompany_EvaluatesBothEvenAfterFirstIsFeasible()
    {
        var companyId = Guid.NewGuid();
        var truckA = ReadyTruck(companyId);
        var truckB = ReadyTruck(companyId);
        var shipment = SomeShipment();
        var harness = new Harness(companyId, [truckA, truckB], shipment);
        harness.RegisterCompany(TruckingCompany.Create(companyId, "Acme Trucking", Office));

        var results = await harness.NewEngine().EvaluateForCompanyAsync(shipment.Id, companyId);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.IsFeasible));
        Assert.Contains(results, r => r.TruckId == truckA.Id);
        Assert.Contains(results, r => r.TruckId == truckB.Id);
    }

    [Fact]
    public async Task EvaluateForCompanyAsync_OnlyEvaluatesRequestedCompanysTrucks()
    {
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var truck = ReadyTruck(companyId);
        var otherCompanyTruck = ReadyTruck(otherCompanyId);
        var shipment = SomeShipment();

        // Harness only wires GetByTruckingCompanyIdAsync for `companyId` - if the engine
        // ever queried the wrong company or the whole fleet, this would return nothing
        // instead of silently including otherCompanyTruck.
        var harness = new Harness(companyId, [truck], shipment);
        harness.RegisterCompany(TruckingCompany.Create(companyId, "Acme Trucking", Office));

        var results = await harness.NewEngine().EvaluateForCompanyAsync(shipment.Id, companyId);

        var result = Assert.Single(results);
        Assert.Equal(truck.Id, result.TruckId);
        Assert.NotEqual(otherCompanyTruck.Id, result.TruckId);
    }

    // --- Regression: Office(return) stop must not be counted as an insertion position ---

    [Fact]
    public async Task EvaluateForCompanyAsync_TripWithOnlyOfficeStopPending_TreatedAsNoPendingStops()
    {
        var companyId = Guid.NewGuid();
        var truck = ReadyTruck(companyId);
        var harness = NewHarnessForOneTruck(truck, SomeShipment());

        // A trip whose only stop is a completed shipment plus the Office(return) stop -
        // no non-Office Pending stops remain, so this must behave like "no pending stops"
        // (search starts at index 0), not attempt an out-of-range index against the
        // Office stop.
        var reachedShipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery,
            Capacity.Create(50, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddHours(1)),
            TimeWindow.Create(StartedAt, StartedAt.AddHours(2)),
            StartedAt);
        harness.Shipments.Setup(s => s.GetByIdAsync(reachedShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(reachedShipment);

        var trip = Trip.Open(truck.Id, companyId, StartedAt);
        var legPlan = new Freight.Domain.Fleet.ValueObjects.LegPlan(
            new Freight.Domain.Fleet.ValueObjects.RouteSegment(20, 6),
            null,
            new Freight.Domain.Fleet.ValueObjects.RouteSegment(20, 6),
            null,
            new Freight.Domain.Fleet.ValueObjects.RouteSegment(20, 6));
        trip.AssignShipment(reachedShipment.Id, reachedShipment.Load, Pickup, Delivery, Office, 0, 0, legPlan);
        truck.BeginTripCompliance(trip.StartedAt);

        // Mark pickup and delivery Reached, leaving only the Office stop Pending.
        var pickupStop = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var deliveryStop = trip.Stops.First(s => s.Kind == StopKind.Delivery);
        trip.MarkStopReached(pickupStop.Id, StartedAt.AddMinutes(30));
        trip.MarkStopReached(deliveryStop.Id, StartedAt.AddHours(1));
        harness.OpenTripByTruckId[truck.Id] = trip;

        var newShipment = SomeShipment();
        harness.Shipments.Setup(s => s.GetByIdAsync(newShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(newShipment);

        var results = await harness.NewEngine().EvaluateForCompanyAsync(newShipment.Id, companyId);

        Assert.True(Assert.Single(results).IsFeasible);
    }
}
