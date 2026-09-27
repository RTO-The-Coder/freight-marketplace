using Freight.Application.Fleet;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Client.Enums;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Tracking.Services;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class AssignShipmentToTruckHandlerTests
{
    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0);
    private static readonly GeoLocation Office = GeoLocation.Create(50.11, 8.68);
    private static readonly GeoLocation Pickup = GeoLocation.Create(52.52, 13.405);
    private static readonly GeoLocation Delivery = GeoLocation.Create(48.1351, 11.582);

    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static Shipment SomeShipment(TruckType requiredType = TruckType.Refrigerated) => Shipment.Book(
        Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery,
        Capacity.Create(100, 1), requiredType,
        TimeWindow.Create(StartedAt, StartedAt.AddDays(1)),
        TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
        StartedAt);

    private static (Truck Truck, TruckingCompany Company) ReadyTruck(TruckType type = TruckType.Refrigerated, TruckSize size = TruckSize.Medium)
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", Office);
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", type, size);
        truck.AssignToCompany(company.Id);
        truck.AssignDrivers(Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules()));
        truck.Activate();
        return (truck, company);
    }

    private sealed class Harness
    {
        public Mock<ITruckRepository> Trucks { get; } = new();
        public Mock<IShipmentRepository> Shipments { get; } = new();
        public Mock<ITruckingCompanyRepository> Companies { get; } = new();
        public Mock<ITripRepository> Trips { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public FakeRoutingService RoutingService { get; } = new();
        public Trip? AddedTrip;

        public Harness(Truck? truck, Shipment? shipment, TruckingCompany? company, Trip? openTrip)
        {
            Trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
            Shipments.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
            Companies.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(company);
            Trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(openTrip);
            Trips.Setup(t => t.Add(It.IsAny<Trip>())).Callback<Trip>(t => AddedTrip = t);

            UnitOfWork.SetupGet(u => u.Trucks).Returns(Trucks.Object);
            UnitOfWork.SetupGet(u => u.Shipments).Returns(Shipments.Object);
            UnitOfWork.SetupGet(u => u.TruckingCompanies).Returns(Companies.Object);
            UnitOfWork.SetupGet(u => u.Trips).Returns(Trips.Object);
            FakeSimulationClock.SetUp(UnitOfWork, StartedAt);
        }

        public AssignShipmentToTruckHandler NewHandler() =>
            new(UnitOfWork.Object, new ShipmentInsertionPlanner(
                UnitOfWork.Object,
                new ShipmentInsertionEvaluator(new RouteEtaCalculator(new DriverRuleEngine())),
                RoutingService,
                new FakeTimeProvider(StartedAt)));
    }

    // --- Happy path: new trip ---

    [Fact]
    public async Task AssignShipmentAsync_NewTrip_OpensTripSeedsComplianceAndAssignsShipment()
    {
        var (truck, company) = ReadyTruck();
        var shipment = SomeShipment();
        var harness = new Harness(truck, shipment, company, null);
        var handler = harness.NewHandler();

        var response = await handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0));

        Assert.Equal(3, response.StopCount); // office + pickup + delivery
        Assert.NotNull(harness.AddedTrip);
        Assert.NotNull(truck.DriverAssignment!.PrimaryDriver.ComplianceState);
        Assert.NotNull(truck.CurrentProgress);
        Assert.Equal(company.Id, shipment.TruckingCompanyId);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- Infeasible insertion: real aggregates untouched ---

    [Fact]
    public async Task AssignShipmentAsync_InfeasibleWindow_ThrowsAndLeavesRealAggregatesUntouched()
    {
        var (truck, company) = ReadyTruck();
        // Pickup window closes before the truck could possibly arrive.
        var tightShipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery,
            Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddMinutes(1)),
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
            StartedAt);
        var harness = new Harness(truck, tightShipment, company, null);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, tightShipment.Id, 0, 0)));

        Assert.Null(truck.DriverAssignment!.PrimaryDriver.ComplianceState);
        Assert.Null(truck.CurrentProgress);
        Assert.Equal(ShipmentStatus.Pending, tightShipment.Status);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- CheckFeasibilityAsync: dry run, side-effect-free ---

    [Fact]
    public async Task CheckFeasibilityAsync_InfeasibleInsertion_ReturnsFalseWithReason_NeverSaves()
    {
        var (truck, company) = ReadyTruck();
        var tightShipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery,
            Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddMinutes(1)),
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
            StartedAt);
        var harness = new Harness(truck, tightShipment, company, null);
        var handler = harness.NewHandler();

        var response = await handler.CheckFeasibilityAsync(new AssignShipmentToTruckRequest(truck.Id, tightShipment.Id, 0, 0));

        Assert.False(response.IsFeasible);
        Assert.NotNull(response.Reason);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckFeasibilityAsync_FeasibleInsertion_ReturnsTrue_NeverSaves()
    {
        var (truck, company) = ReadyTruck();
        var shipment = SomeShipment();
        var harness = new Harness(truck, shipment, company, null);
        var handler = harness.NewHandler();

        var response = await handler.CheckFeasibilityAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0));

        Assert.True(response.IsFeasible);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Second shipment on already-open trip: compliance NOT reset ---

    [Fact]
    public async Task AssignShipmentAsync_SecondShipmentOnOpenTrip_DoesNotResetComplianceLedger()
    {
        var (truck, company) = ReadyTruck();
        var firstShipment = SomeShipment();
        var harness1 = new Harness(truck, firstShipment, company, null);
        var handler1 = harness1.NewHandler();
        await handler1.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, firstShipment.Id, 0, 0));

        var openTrip = harness1.AddedTrip!;
        // Mutate the ledger to a known non-fresh value so a reset would be detectable.
        var ledgerBefore = truck.DriverAssignment!.PrimaryDriver.ComplianceState;
        var lastEvaluatedBefore = ledgerBefore!.LastEvaluatedSimulatedTime;

        var secondShipment = SomeShipment();
        var harness2 = new Harness(truck, secondShipment, company, openTrip);
        var handler2 = harness2.NewHandler();

        await handler2.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, secondShipment.Id, 2, 2));

        // Same ledger instance, not replaced by a fresh ResetComplianceForNewTrip call.
        Assert.Same(ledgerBefore, truck.DriverAssignment.PrimaryDriver.ComplianceState);
    }

    // --- Guards short-circuit before any routing call or save ---

    [Fact]
    public async Task AssignShipmentAsync_UnknownTruckId_Throws_NoRoutingCallsNoSave()
    {
        var harness = new Harness(null, null, null, null);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(Guid.NewGuid(), Guid.NewGuid(), 0, 0)));

        Assert.Empty(harness.RoutingService.Requests);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignShipmentAsync_UnknownShipmentId_Throws_NoRoutingCalls()
    {
        var (truck, _) = ReadyTruck();
        var harness = new Harness(truck, null, null, null);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, Guid.NewGuid(), 0, 0)));

        Assert.Empty(harness.RoutingService.Requests);
    }

    [Fact]
    public async Task AssignShipmentAsync_TruckHasNoCompany_Throws_NoRoutingCalls()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        var shipment = SomeShipment();
        var harness = new Harness(truck, shipment, null, null);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0)));

        Assert.Empty(harness.RoutingService.Requests);
    }

    [Fact]
    public async Task AssignShipmentAsync_TruckInactive_Throws_NoRoutingCalls()
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", Office);
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(company.Id);
        truck.AssignDrivers(Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules()));
        // Not activated.
        var shipment = SomeShipment();
        var harness = new Harness(truck, shipment, company, null);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0)));

        Assert.Empty(harness.RoutingService.Requests);
    }

    [Fact]
    public async Task AssignShipmentAsync_TruckTypeMismatch_Throws_NoRoutingCalls()
    {
        var (truck, company) = ReadyTruck(type: TruckType.BoxVan);
        var shipment = SomeShipment(requiredType: TruckType.Refrigerated);
        var harness = new Harness(truck, shipment, company, null);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0)));

        Assert.Empty(harness.RoutingService.Requests);
    }

    [Fact]
    public async Task AssignShipmentAsync_NoDriverAssignment_Throws_NoRoutingCalls()
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", Office);
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(company.Id);
        var shipment = SomeShipment();
        var harness = new Harness(truck, shipment, company, null);
        var handler = harness.NewHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0)));

        Assert.Empty(harness.RoutingService.Requests);
    }

    // --- RouteStartLocation: office vs interpolated live position ---

    [Fact]
    public async Task AssignShipmentAsync_SecondShipmentWhileTruckAlreadyDriving_UsesInterpolatedPositionNotOffice()
    {
        var (truck, company) = ReadyTruck();
        var firstShipment = SomeShipment();
        var harness1 = new Harness(truck, firstShipment, company, null);
        var handler1 = harness1.NewHandler();
        await handler1.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, firstShipment.Id, 0, 0));
        var openTrip = harness1.AddedTrip!;

        // Truck has started driving toward the first pickup.
        truck.CurrentProgress!.AdvanceByTicks(3);

        var secondShipment = SomeShipment();
        var harness2 = new Harness(truck, secondShipment, company, openTrip);
        var handler2 = harness2.NewHandler();

        // Insert the second shipment AHEAD of the truck's current position (index 0).
        await handler2.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, secondShipment.Id, 0, 0));

        // The pickup-incoming leg's "from" must be the interpolated position, not the office.
        var pickupIncomingCall = harness2.RoutingService.Requests.First();
        Assert.NotEqual(Office, pickupIncomingCall.From);
    }

    [Fact]
    public async Task CheckFeasibilityAsync_PickupInsertedAheadOfMovingTruck_PreviewDrivesTheNewLegNotTheOldLegsRemainder()
    {
        // The truck is 3 of 120 ticks (10h) into its first leg. A pickup inserted ahead of
        // it is 6 ticks (30 min) from the live position and must be picked up within 1h.
        // The preview must drive that fresh 30-min leg - not the 117 ticks left on the old
        // leg, which would push the pickup past its window.
        var (truck, company) = ReadyTruck();
        var firstShipment = SomeShipment();
        var harness1 = new Harness(truck, firstShipment, company, null);
        harness1.RoutingService.DefaultLeg = new RouteLeg(DistanceKm: 1000, TimeTicks: 120);
        await harness1.NewHandler().AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, firstShipment.Id, 0, 0));
        var openTrip = harness1.AddedTrip!;
        truck.CurrentProgress!.AdvanceByTicks(3);

        var urgentShipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), Pickup, Delivery,
            Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddHours(1)),
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
            StartedAt);
        var harness2 = new Harness(truck, urgentShipment, company, openTrip);
        harness2.RoutingService.DefaultLeg = new RouteLeg(DistanceKm: 25, TimeTicks: 6);
        // The first shipment keeps its own (wide) windows in the preview.
        harness2.Shipments.Setup(sh => sh.GetByIdAsync(firstShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(firstShipment);

        var response = await harness2.NewHandler().CheckFeasibilityAsync(
            new AssignShipmentToTruckRequest(truck.Id, urgentShipment.Id, 0, 0));

        Assert.True(response.IsFeasible, response.Reason);
    }

    [Fact]
    public async Task AssignShipmentAsync_NewTrip_RouteStartsFromOffice()
    {
        var (truck, company) = ReadyTruck();
        var shipment = SomeShipment();
        var harness = new Harness(truck, shipment, company, null);
        var handler = harness.NewHandler();

        await handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0));

        var pickupIncomingCall = harness.RoutingService.Requests.First();
        Assert.Equal(Office, pickupIncomingCall.From);
    }

    // --- Follower legs: requested only when inserting before an existing pending stop ---

    [Fact]
    public async Task AssignShipmentAsync_InsertedAtEnd_DoesNotRequestFollowerLegs()
    {
        var (truck, company) = ReadyTruck();
        var firstShipment = SomeShipment();
        var harness1 = new Harness(truck, firstShipment, company, null);
        var handler1 = harness1.NewHandler();
        await handler1.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, firstShipment.Id, 0, 0));
        var openTrip = harness1.AddedTrip!;

        var secondShipment = SomeShipment();
        var harness2 = new Harness(truck, secondShipment, company, openTrip);
        var handler2 = harness2.NewHandler();

        // Insert appended at the end (index 2 = after existing pickup+delivery).
        await handler2.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, secondShipment.Id, 2, 2));

        // Appended stops displace nothing, so no follower leg is measured: the new pickup
        // is followed by the new delivery, never by an existing stop or the office. (The
        // office return leg IS re-measured, from the new delivery - see
        // AssignShipmentAsync_SecondShipmentAppendedAtEnd_RemeasuresOfficeLegFromNewLastStop.)
        Assert.DoesNotContain(harness2.RoutingService.Requests, c => c.From == Pickup && c.To == Office);
    }

    [Fact]
    public async Task AssignShipmentAsync_InsertedBeforeExistingPendingStop_RequestsFollowerLeg()
    {
        var (truck, company) = ReadyTruck();
        var firstShipment = SomeShipment();
        var harness1 = new Harness(truck, firstShipment, company, null);
        var handler1 = harness1.NewHandler();
        await handler1.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, firstShipment.Id, 0, 0));
        var openTrip = harness1.AddedTrip!;
        var firstPickup = openTrip.Stops.First(s => s.Kind == StopKind.Pickup);

        var secondShipment = SomeShipment();
        var harness2 = new Harness(truck, secondShipment, company, openTrip);
        var handler2 = harness2.NewHandler();

        // Insert BEFORE the existing pending pickup (index 0) - a follower leg (new
        // pickup -> old pickup) must be requested.
        await handler2.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, secondShipment.Id, 0, 0));

        Assert.Contains(harness2.RoutingService.Requests, c => c.From == Pickup && c.To == firstPickup.Location);
    }

    // --- Office return leg follows the trip's last stop ---

    [Fact]
    public async Task AssignShipmentAsync_SecondShipmentAppendedAtEnd_RemeasuresOfficeLegFromNewLastStop()
    {
        var (truck, company) = ReadyTruck();
        var firstShipment = SomeShipment();
        var harness1 = new Harness(truck, firstShipment, company, null);
        await harness1.NewHandler().AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, firstShipment.Id, 0, 0));
        var openTrip = harness1.AddedTrip!;

        // Second shipment at its own locations, so its delivery becomes a new last stop.
        var secondPickup = GeoLocation.Create(51.05, 13.74);
        var secondDelivery = GeoLocation.Create(53.55, 9.99);
        var secondShipment = Shipment.Book(
            Guid.NewGuid(), Guid.NewGuid(), secondPickup, secondDelivery,
            Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(StartedAt, StartedAt.AddDays(1)),
            TimeWindow.Create(StartedAt, StartedAt.AddDays(2)),
            StartedAt);

        var harness2 = new Harness(truck, secondShipment, company, openTrip);
        var returnFromNewLastStop = new RouteLeg(DistanceKm: 90, TimeTicks: 11);
        harness2.RoutingService.LegFor[(secondDelivery, Office)] = returnFromNewLastStop;

        // Appended after the first shipment's pickup and delivery.
        await harness2.NewHandler().AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, secondShipment.Id, 2, 2));

        // The planner measured the return from the new last stop...
        Assert.Contains(harness2.RoutingService.Requests, c => c.From == secondDelivery && c.To == Office);

        // ...and the trip now returns to the office on that leg, not the first shipment's.
        var officeStop = openTrip.Stops.Single(stop => stop.Kind == StopKind.Office);
        Assert.Equal(returnFromNewLastStop.DistanceKm, officeStop.IncomingLegDistanceKm);
        Assert.Equal(returnFromNewLastStop.TimeTicks, officeStop.IncomingLegTimeTick);
    }
}
