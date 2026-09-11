using Freight.Application.Simulation;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Simulation;

public sealed class SimulationAdvanceHandlerTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);
    private static readonly GeoLocation Office = GeoLocation.Create(50.11, 8.68);
    private static readonly GeoLocation Pickup = GeoLocation.Create(52.52, 13.405);
    private static readonly GeoLocation Delivery = GeoLocation.Create(48.1351, 11.582);
    private readonly DriverRuleEngine _engine = new();

    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    /// <summary>A truck+trip+shipment ready to advance: driver(s) assigned, compliance seeded, CurrentProgress synced, one shipment inserted.</summary>
    private sealed class Fixture
    {
        public required Truck Truck;
        public required Trip Trip;
        public required Shipment Shipment;
        public required Stop PickupStop;
        public required Stop DeliveryStop;
    }

    private static Fixture SingleDriverFixture(
        int pickupIncomingTicks = 6, int deliveryIncomingTicks = 6, int toOfficeTicks = 6,
        Capacity? shipmentLoad = null, TruckSize truckSize = TruckSize.Medium)
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, truckSize);
        var companyId = Guid.NewGuid();
        truck.AssignToCompany(companyId);
        truck.AssignDrivers(Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules()));
        truck.BeginTripCompliance(StartedAt);

        var trip = Trip.Open(truck.Id, companyId, StartedAt);
        var load = shipmentLoad ?? Capacity.Create(100, 1);
        var shipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery, load, TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
            TimeWindow.Create(StartedAt, StartedAt.AddDays(3)),
            StartedAt);
        shipment.AssignToCompany(companyId);

        trip.AssignShipment(
            shipment.Id, load, Pickup, Delivery, Office,
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            new LegPlan(
                new RouteSegment(20, pickupIncomingTicks), null,
                new RouteSegment(20, deliveryIncomingTicks), null,
                new RouteSegment(20, toOfficeTicks)));

        truck.SyncProgressToNextStop(trip, previousNextStopId: null);

        var pickupStop = trip.Stops.First(s => s.Kind == StopKind.Pickup);
        var deliveryStop = trip.Stops.First(s => s.Kind == StopKind.Delivery);

        return new Fixture { Truck = truck, Trip = trip, Shipment = shipment, PickupStop = pickupStop, DeliveryStop = deliveryStop };
    }

    private sealed class Harness
    {
        public Mock<ITripRepository> Trips { get; } = new();
        public Mock<ITruckRepository> Trucks { get; } = new();
        public Mock<IShipmentRepository> Shipments { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public Harness(IReadOnlyList<Trip> openTrips, IReadOnlyDictionary<Guid, Truck> trucksById, IReadOnlyDictionary<Guid, Shipment> shipmentsById, DateTime clockStart)
        {
            Trips.Setup(t => t.GetOpenTripsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(openTrips);
            Trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => trucksById.GetValueOrDefault(id));
            Shipments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid id, CancellationToken _) => shipmentsById.GetValueOrDefault(id));

            UnitOfWork.SetupGet(u => u.Trips).Returns(Trips.Object);
            UnitOfWork.SetupGet(u => u.Trucks).Returns(Trucks.Object);
            UnitOfWork.SetupGet(u => u.Shipments).Returns(Shipments.Object);
            FakeSimulationClock.SetUp(UnitOfWork, clockStart);
        }

        public SimulationAdvanceHandler NewHandler() =>
            new(UnitOfWork.Object, new DriverRuleEngine(), new FakeTimeProvider(DateTimeOffset.UtcNow));
    }

    // --- Basic validation ---

    [Fact]
    public async Task AdvanceSimulationAsync_NegativeTicks_Throws_ClockUntouched_NeverSaves()
    {
        var harness = new Harness([], new Dictionary<Guid, Truck>(), new Dictionary<Guid, Shipment>(), StartedAt);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(-1)));
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AdvanceSimulationAsync_ZeroTicks_ClockAdvancesByZero_NoTripsAdvanced()
    {
        var harness = new Harness([], new Dictionary<Guid, Truck>(), new Dictionary<Guid, Shipment>(), StartedAt);
        var handler = harness.NewHandler();

        var response = await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(0));

        Assert.Equal(StartedAt, response.CurrentTime);
        Assert.Equal(0, response.TripsAdvanced);
        Assert.Equal(0, response.TripsCompleted);
    }

    // --- Single-driver: leg completes with no wait ---

    [Fact]
    public async Task AdvanceSimulationAsync_SingleDriver_LegCompletesNoWait_StopReachedOnCompletingTick()
    {
        var fixture = SingleDriverFixture(pickupIncomingTicks: 3);
        var harness = new Harness(
            [fixture.Trip],
            new Dictionary<Guid, Truck> { [fixture.Truck.Id] = fixture.Truck },
            new Dictionary<Guid, Shipment> { [fixture.Shipment.Id] = fixture.Shipment },
            StartedAt);
        var handler = harness.NewHandler();

        var response = await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(3));

        Assert.Equal(StopStatus.Reached, fixture.PickupStop.Status);
        Assert.Equal(StartedAt.AddMinutes(15), fixture.PickupStop.ReachedAt);
        Assert.Equal(ShipmentStatus.InTransit, fixture.Shipment.Status);
        Assert.Equal(1, response.TripsAdvanced);
        Assert.Equal(0, response.TripsCompleted);
    }

    [Fact]
    public async Task AdvanceSimulationAsync_DeliveryStopReached_MarksShipmentDelivered()
    {
        var fixture = SingleDriverFixture(pickupIncomingTicks: 2, deliveryIncomingTicks: 2, toOfficeTicks: 2);
        var harness = new Harness(
            [fixture.Trip],
            new Dictionary<Guid, Truck> { [fixture.Truck.Id] = fixture.Truck },
            new Dictionary<Guid, Shipment> { [fixture.Shipment.Id] = fixture.Shipment },
            StartedAt);
        var handler = harness.NewHandler();

        await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(4));

        Assert.Equal(StopStatus.Reached, fixture.DeliveryStop.Status);
        Assert.Equal(ShipmentStatus.Delivered, fixture.Shipment.Status);
    }

    [Fact]
    public async Task AdvanceSimulationAsync_TripReachesOfficeStop_CompletesTrip()
    {
        var fixture = SingleDriverFixture(pickupIncomingTicks: 1, deliveryIncomingTicks: 1, toOfficeTicks: 1);
        var harness = new Harness(
            [fixture.Trip],
            new Dictionary<Guid, Truck> { [fixture.Truck.Id] = fixture.Truck },
            new Dictionary<Guid, Shipment> { [fixture.Shipment.Id] = fixture.Shipment },
            StartedAt);
        var handler = harness.NewHandler();

        var response = await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(10));

        Assert.False(fixture.Trip.IsOpen);
        Assert.Equal(1, response.TripsCompleted);
    }

    // --- Wait-for-window: park, serve ticks, then reach ---

    [Fact]
    public async Task AdvanceSimulationAsync_StopWithPlannedWait_ParksAndServesWaitBeforeReaching()
    {
        var fixture = SingleDriverFixture(pickupIncomingTicks: 2);
        fixture.Trip.SetPlannedWaits(new Dictionary<StopRef, int>
        {
            [StopRef.For(fixture.PickupStop)] = 3,
        });
        var harness = new Harness(
            [fixture.Trip],
            new Dictionary<Guid, Truck> { [fixture.Truck.Id] = fixture.Truck },
            new Dictionary<Guid, Shipment> { [fixture.Shipment.Id] = fixture.Shipment },
            StartedAt);
        var handler = harness.NewHandler();

        // 2 ticks to complete the leg + 2 ticks of wait (not yet complete: needs 3).
        await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(4));

        Assert.Equal(StopStatus.Pending, fixture.PickupStop.Status);
        Assert.Equal(2, fixture.PickupStop.WaitTimeTickElapsed);

        // One more tick completes the wait and reaches the stop.
        var response = await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(1));
        Assert.Equal(StopStatus.Reached, fixture.PickupStop.Status);
        Assert.Equal(1, response.TripsAdvanced);
    }

    // --- Capacity violation at pickup ---

    [Fact]
    public async Task AdvanceSimulationAsync_PickupWouldExceedWeightCapacity_Throws()
    {
        var mediumCap = Capacity.ForTruckSize(TruckSize.Medium);
        var oversizedLoad = Capacity.Create(mediumCap.WeightKg + 1, mediumCap.VolumeCubicMeters);
        var fixture = SingleDriverFixture(pickupIncomingTicks: 1, shipmentLoad: oversizedLoad, truckSize: TruckSize.Medium);
        var harness = new Harness(
            [fixture.Trip],
            new Dictionary<Guid, Truck> { [fixture.Truck.Id] = fixture.Truck },
            new Dictionary<Guid, Shipment> { [fixture.Shipment.Id] = fixture.Shipment },
            StartedAt);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(1)));
    }

    [Fact]
    public async Task AdvanceSimulationAsync_PickupWouldExceedVolumeCapacity_Throws()
    {
        var mediumCapacity = Capacity.ForTruckSize(TruckSize.Medium);
        var oversizedLoad = Capacity.Create(mediumCapacity.WeightKg, mediumCapacity.VolumeCubicMeters + 1);
        var fixture = SingleDriverFixture(pickupIncomingTicks: 1, shipmentLoad: oversizedLoad, truckSize: TruckSize.Medium);
        var harness = new Harness(
            [fixture.Trip],
            new Dictionary<Guid, Truck> { [fixture.Truck.Id] = fixture.Truck },
            new Dictionary<Guid, Shipment> { [fixture.Shipment.Id] = fixture.Shipment },
            StartedAt);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(1)));
    }

    // --- Team driver: swap and PersistActiveDriver guard ---

    [Fact]
    public async Task AdvanceSimulationAsync_TeamDriver_SwapsActiveDriverWhenPrimaryHitsCap()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Large);
        var companyId = Guid.NewGuid();
        truck.AssignToCompany(companyId);
        var primary = Driver.Create(Guid.NewGuid(), "Primary", "Driver", SomeRules());
        var secondary = Driver.Create(Guid.NewGuid(), "Secondary", "Driver", SomeRules());
        truck.AssignDrivers(primary, secondary);
        truck.BeginTripCompliance(StartedAt);
        // Force primary near the daily cap so a swap is required almost immediately - drive
        // them (via the real engine, publicly, tick by tick so any intervening breaks are
        // correctly served rather than assuming elapsed time equals driving time) until
        // within 5 minutes of the daily cap.
        var limits = Freight.Domain.Tracking.ValueObjects.RestRuleLimits.Default;
        var target = limits.MaxDailyDrivingMinutes - 5;
        var now = StartedAt;
        while (primary.ComplianceState!.DailyDrivingMinutesToday < target)
        {
            now = now.AddMinutes(5);
            _engine.Advance(primary.ComplianceState, TimeSpan.FromMinutes(5), now, primary.Rules, limits);
        }

        var trip = Trip.Open(truck.Id, companyId, StartedAt);
        var shipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery, Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)), TimeWindow.Create(StartedAt, StartedAt.AddDays(3)), StartedAt);
        shipment.AssignToCompany(companyId);
        trip.AssignShipment(
            shipment.Id, shipment.Load, Pickup, Delivery, Office,
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            new LegPlan(new RouteSegment(20, 20), null, new RouteSegment(20, 6), null, new RouteSegment(20, 6)));
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);

        var harness = new Harness(
            [trip],
            new Dictionary<Guid, Truck> { [truck.Id] = truck },
            new Dictionary<Guid, Shipment> { [shipment.Id] = shipment },
            StartedAt);
        var handler = harness.NewHandler();

        await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(4));

        Assert.Equal(secondary.Id, truck.DriverAssignment!.ActiveDriverId);
    }

    [Fact]
    public async Task AdvanceSimulationAsync_TeamDriver_NoSwapNeeded_DoesNotCallSetActiveDriver()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Large);
        var companyId = Guid.NewGuid();
        truck.AssignToCompany(companyId);
        var primary = Driver.Create(Guid.NewGuid(), "Primary", "Driver", SomeRules());
        var secondary = Driver.Create(Guid.NewGuid(), "Secondary", "Driver", SomeRules());
        truck.AssignDrivers(primary, secondary);
        truck.BeginTripCompliance(StartedAt);

        var trip = Trip.Open(truck.Id, companyId, StartedAt);
        var shipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery, Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)), TimeWindow.Create(StartedAt, StartedAt.AddDays(3)), StartedAt);
        shipment.AssignToCompany(companyId);
        trip.AssignShipment(
            shipment.Id, shipment.Load, Pickup, Delivery, Office,
            pickupInsertIndex: 0, deliveryInsertIndex: 0,
            new LegPlan(new RouteSegment(20, 3), null, new RouteSegment(20, 3), null, new RouteSegment(20, 3)));
        truck.SyncProgressToNextStop(trip, previousNextStopId: null);
        var originalAssignment = truck.DriverAssignment;

        var harness = new Harness(
            [trip],
            new Dictionary<Guid, Truck> { [truck.Id] = truck },
            new Dictionary<Guid, Shipment> { [shipment.Id] = shipment },
            StartedAt);
        var handler = harness.NewHandler();

        await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(3));

        // ActiveDriverId stayed on primary the whole time - no swap, and (implicitly)
        // SetActiveDriver was never called with a backward/invalid move (which would throw).
        Assert.Equal(primary.Id, truck.DriverAssignment!.ActiveDriverId);
        Assert.Same(originalAssignment, truck.DriverAssignment);
    }

    // --- Trip not yet departed ---

    [Fact]
    public async Task AdvanceSimulationAsync_TripStartsMidWindow_OnlyMovesForTicksAfterStartedAt()
    {
        var fixture = SingleDriverFixture(pickupIncomingTicks: 2);
        // Trip's planned departure is 2 ticks (10 min) after the window starts.
        fixture.Trip.Reschedule(StartedAt.AddMinutes(10), truckHasStartedDriving: false);
        var harness = new Harness(
            [fixture.Trip],
            new Dictionary<Guid, Truck> { [fixture.Truck.Id] = fixture.Truck },
            new Dictionary<Guid, Shipment> { [fixture.Shipment.Id] = fixture.Shipment },
            StartedAt);
        var handler = harness.NewHandler();

        // 3 ticks total in the window: first 2 are skipped (before StartedAt), only the
        // 3rd tick actually drives - not enough to complete a 2-tick leg yet.
        await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(3));

        Assert.Equal(1, fixture.Truck.CurrentProgress!.CurrentDrivingTimeTick);
    }

    // --- Multiple open trips: independent advancement ---

    [Fact]
    public async Task AdvanceSimulationAsync_MultipleOpenTrips_EachAdvancesIndependently()
    {
        var fixtureA = SingleDriverFixture(pickupIncomingTicks: 2);
        var fixtureB = SingleDriverFixture(pickupIncomingTicks: 5);
        var harness = new Harness(
            [fixtureA.Trip, fixtureB.Trip],
            new Dictionary<Guid, Truck> { [fixtureA.Truck.Id] = fixtureA.Truck, [fixtureB.Truck.Id] = fixtureB.Truck },
            new Dictionary<Guid, Shipment> { [fixtureA.Shipment.Id] = fixtureA.Shipment, [fixtureB.Shipment.Id] = fixtureB.Shipment },
            StartedAt);
        var handler = harness.NewHandler();

        var response = await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(2));

        // A's 2-tick leg completed; B's 5-tick leg did not.
        Assert.Equal(StopStatus.Reached, fixtureA.PickupStop.Status);
        Assert.Equal(StopStatus.Pending, fixtureB.PickupStop.Status);
        Assert.Equal(2, fixtureB.Truck.CurrentProgress!.CurrentDrivingTimeTick);
        Assert.Equal(2, response.TripsAdvanced);
    }

    [Fact]
    public async Task AdvanceSimulationAsync_AdvancesClockByRequestedTicks()
    {
        var harness = new Harness([], new Dictionary<Guid, Truck>(), new Dictionary<Guid, Shipment>(), StartedAt);
        var handler = harness.NewHandler();

        var response = await handler.AdvanceSimulationAsync(new AdvanceSimulationRequest(12));

        Assert.Equal(StartedAt.AddHours(1), response.CurrentTime);
    }
}
