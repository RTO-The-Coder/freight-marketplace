using Freight.Application.Fleet;
using Freight.Application.Tests;
using Freight.Application.Tests.Shipments;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.Client;
using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;
using ShipmentAggregate = Freight.Domain.Client.Shipment;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Tracking.Services;
using Freight.Domain.Client.Enums;

namespace Freight.Application.Tests.Fleet;

public sealed class AssignShipmentToTruckHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Driver NewDriver()
    {
        var driver = Driver.Create(
            "Jane",
            "Doe",
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));
        driver.ResetComplianceForNewTrip(Now);
        return driver;
    }

    private static TruckingCompany NewCompany() =>
        TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", GeoLocation.Create(52.52, 13.405));

    // Each placeholder leg is 78 ticks = 6.5h. Two consecutive legs (13h continuous
    // driving) exceed any legal single-day cap (max 10h even with extended driving), so
    // the evaluator's driver-hours check requires the driver to take a mandatory rest
    // somewhere in between - windows are deliberately wide (days, not hours) so these
    // tests aren't coupled to the evaluator's current known gap (it projects arrival via
    // a naive tick-sum that doesn't yet fold in that rest time - see the handler's
    // commit message / plan notes). A tight multi-leg-same-day window would require that
    // gap to be fixed first.
    private static ShipmentAggregate NewShipment(TruckType requiredType = TruckType.BoxVan, Capacity? load = null) =>
        ShipmentAggregate.Book(
            Guid.NewGuid(),
            GeoLocation.Create(52.5, 13.4),
            GeoLocation.Create(48.1, 11.6),
            load ?? Capacity.Create(100, 2),
            requiredType,
            TimeWindow.Create(Now, Now.AddDays(7)),
            TimeWindow.Create(Now.AddHours(6), Now.AddDays(7)),
            Now);

    private static (
        Mock<IUnitOfWork> UnitOfWork,
        Mock<ITruckRepository> Trucks,
        Mock<ITripRepository> Trips,
        Mock<IShipmentRepository> Shipments,
        Mock<ITruckingCompanyRepository> Companies) NewMocks()
    {
        var trucks = new Mock<ITruckRepository>();
        var trips = new Mock<ITripRepository>();
        var shipments = new Mock<IShipmentRepository>();
        var companies = new Mock<ITruckingCompanyRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        unitOfWork.SetupGet(u => u.Shipments).Returns(shipments.Object);
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);
        FakeSimulationClock.SetUp(unitOfWork, Now);
        return (unitOfWork, trucks, trips, shipments, companies);
    }

    private static Truck NewAssignableTruck(TruckingCompany company, out Driver driver, TruckType type = TruckType.BoxVan)
    {
        var truck = Truck.Create("Truck 1", type, TruckSize.Medium);
        truck.AssignToCompany(company.Id);
        driver = NewDriver();
        truck.AssignDrivers(driver);
        truck.Activate();
        return truck;
    }

    private static AssignShipmentToTruckHandler NewHandler(IUnitOfWork unitOfWork) =>
        NewHandler(unitOfWork, new FakeRoutingService());

    private static AssignShipmentToTruckHandler NewHandler(IUnitOfWork unitOfWork, IRoutingService routingService) =>
        new(unitOfWork,
            new ShipmentInsertionEvaluator(new RouteEtaCalculator(new DriverRuleEngine())),
            routingService,
            new FakeTimeProvider(Now));

    [Fact]
    public async Task HandleAsync_ValidRequest_OpensTripInsertsThreeStopsAndStartsDrivingAndSaves()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out var driver);
        var shipment = NewShipment();

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var handler = NewHandler(unitOfWork.Object);

        var response = await handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0));

        Assert.Equal(3, response.StopCount);
        Assert.Equal(ShipmentStatus.Booked, shipment.Status);
        Assert.Equal(company.Id, shipment.TruckingCompanyId);
        Assert.NotNull(driver.ComplianceState);
        Assert.NotNull(truck.CurrentProgress);
        Assert.Equal(650, truck.CurrentProgress!.TotalDistanceKm);
        Assert.Equal(78, truck.CurrentProgress.TotalTimeTick);
        trips.Verify(t => t.Add(It.Is<Trip>(trip => trip.TruckId == truck.Id)), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TruckTypeMismatch_ThrowsAndDoesNotSave()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out _, type: TruckType.Flatbed);
        var shipment = NewShipment(requiredType: TruckType.Refrigerated);

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var handler = NewHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0)));

        trips.Verify(t => t.Add(It.IsAny<Trip>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ExceedsCapacity_ThrowsAndDoesNotSave()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out _);
        var oversizedShipment = NewShipment(load: Capacity.Create(truck.Capacity.WeightKg + 1, 5));

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(oversizedShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(oversizedShipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var handler = NewHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, oversizedShipment.Id, 0, 0)));

        trips.Verify(t => t.Add(It.IsAny<Trip>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_SecondShipment_InsertsBeforeExistingOfficeStop()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out _);
        var firstShipment = NewShipment();
        var secondShipment = NewShipment();

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(firstShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(firstShipment);
        shipments.Setup(s => s.GetByIdAsync(secondShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(secondShipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        Trip? openTrip = null;
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => openTrip);
        trips.Setup(t => t.Add(It.IsAny<Trip>())).Callback<Trip>(trip => openTrip = trip);

        var handler = NewHandler(unitOfWork.Object);

        await handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, firstShipment.Id, 0, 0));
        var officeStopId = openTrip!.Stops.Single(s => s.Kind == StopKind.Office).Id;

        await handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, secondShipment.Id, 2, 2));

        Assert.Equal(
            [StopKind.Pickup, StopKind.Delivery, StopKind.Pickup, StopKind.Delivery, StopKind.Office],
            openTrip.Stops.Select(s => s.Kind));
        Assert.Equal(officeStopId, openTrip.Stops[^1].Id);
    }

    [Fact]
    public async Task HandleAsync_PickupWindowAlreadyPassedByProjectedArrival_ThrowsAndDoesNotSave()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out _);

        // Window closes before the placeholder 78-tick (6.5h) leg could possibly arrive.
        var infeasibleShipment = ShipmentAggregate.Book(
            Guid.NewGuid(),
            GeoLocation.Create(52.5, 13.4),
            GeoLocation.Create(48.1, 11.6),
            Capacity.Create(100, 2),
            TruckType.BoxVan,
            TimeWindow.Create(Now, Now.AddHours(1)),
            TimeWindow.Create(Now.AddHours(2), Now.AddHours(3)),
            Now);

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(infeasibleShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infeasibleShipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var handler = NewHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, infeasibleShipment.Id, 0, 0)));

        trips.Verify(t => t.Add(It.IsAny<Trip>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckFeasibility_ViableInsertion_ReturnsFeasibleAndSavesNothing()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out _);
        var shipment = NewShipment();

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var handler = NewHandler(unitOfWork.Object);

        var result = await handler.CheckFeasibilityAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0));

        Assert.True(result.IsFeasible);
        Assert.Null(result.Reason);
        trips.Verify(t => t.Add(It.IsAny<Trip>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckFeasibility_PickupWindowUnreachable_ReturnsInfeasibleWithReasonAndSavesNothing()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out _);

        var infeasibleShipment = ShipmentAggregate.Book(
            Guid.NewGuid(),
            GeoLocation.Create(52.5, 13.4),
            GeoLocation.Create(48.1, 11.6),
            Capacity.Create(100, 2),
            TruckType.BoxVan,
            TimeWindow.Create(Now, Now.AddHours(1)),
            TimeWindow.Create(Now.AddHours(2), Now.AddHours(3)),
            Now);

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(infeasibleShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(infeasibleShipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var handler = NewHandler(unitOfWork.Object);

        var result = await handler.CheckFeasibilityAsync(new AssignShipmentToTruckRequest(truck.Id, infeasibleShipment.Id, 0, 0));

        Assert.False(result.IsFeasible);
        Assert.NotNull(result.Reason);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckFeasibility_UnknownTruckId_Throws()
    {
        var (unitOfWork, trucks, _, _, _) = NewMocks();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Truck?)null);

        var handler = NewHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.CheckFeasibilityAsync(new AssignShipmentToTruckRequest(Guid.NewGuid(), Guid.NewGuid(), 0, 0)));
    }

    [Fact]
    public async Task HandleAsync_UnknownTruckId_Throws()
    {
        var (unitOfWork, trucks, _, _, _) = NewMocks();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Truck?)null);

        var handler = NewHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(Guid.NewGuid(), Guid.NewGuid(), 0, 0)));
    }

    [Fact]
    public async Task HandleAsync_NewTrip_MeasuresPickupLegFromCompanyOffice()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out _);
        var shipment = NewShipment();

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var routing = new FakeRoutingService();
        var handler = NewHandler(unitOfWork.Object, routing);

        await handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0));

        // A brand-new trip starts at the company office, so the first leg measured is
        // office -> pickup.
        var firstLeg = routing.Requests[0];
        Assert.Equal(company.OfficeLocation.Latitude, firstLeg.From.Latitude, precision: 9);
        Assert.Equal(company.OfficeLocation.Longitude, firstLeg.From.Longitude, precision: 9);
        Assert.Equal(shipment.PickupLocation.Latitude, firstLeg.To.Latitude, precision: 9);
        Assert.Equal(shipment.PickupLocation.Longitude, firstLeg.To.Longitude, precision: 9);
    }

    [Fact]
    public async Task HandleAsync_PickupInsertedAheadOfMovingTruck_MeasuresPickupLegFromInterpolatedPosition()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out _);
        var firstShipment = NewShipment();
        var secondShipment = NewShipment();

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(firstShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(firstShipment);
        shipments.Setup(s => s.GetByIdAsync(secondShipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(secondShipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        Trip? openTrip = null;
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => openTrip);
        trips.Setup(t => t.Add(It.IsAny<Trip>())).Callback<Trip>(trip => openTrip = trip);

        var routing = new FakeRoutingService();
        var handler = NewHandler(unitOfWork.Object, routing);

        // First shipment opens the trip; truck is now driving the office -> first pickup leg.
        await handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, firstShipment.Id, 0, 0));

        // Drive the truck half-way along its current leg.
        truck.CurrentProgress!.AdvanceByTicks(truck.CurrentProgress.TotalTimeTick / 2);

        routing.Requests.Clear();

        // Insert the second shipment's pickup ahead of the truck (index 0).
        await handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, secondShipment.Id, 0, 0));

        // The pickup leg's start is the truck's live position - the interpolated point
        // half-way between the office and the first pickup, not either endpoint.
        var pickupLeg = routing.Requests[0];
        var expected = company.OfficeLocation.InterpolateTo(firstShipment.PickupLocation, 0.5);
        Assert.Equal(expected.Latitude, pickupLeg.From.Latitude, precision: 6);
        Assert.Equal(expected.Longitude, pickupLeg.From.Longitude, precision: 6);
        Assert.NotEqual(company.OfficeLocation.Latitude, pickupLeg.From.Latitude, precision: 6);
    }

    [Fact]
    public async Task HandleAsync_RoutingUnavailable_ThrowsAndDoesNotSave()
    {
        var (unitOfWork, trucks, trips, shipments, companies) = NewMocks();
        var company = NewCompany();
        var truck = NewAssignableTruck(company, out _);
        var shipment = NewShipment();

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        shipments.Setup(s => s.GetByIdAsync(shipment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
        companies.Setup(c => c.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var routing = new FakeRoutingService
        {
            ThrowOnCall = new RoutingUnavailableException("OSRM down"),
        };
        var handler = NewHandler(unitOfWork.Object, routing);

        await Assert.ThrowsAsync<RoutingUnavailableException>(() =>
            handler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truck.Id, shipment.Id, 0, 0)));

        trips.Verify(t => t.Add(It.IsAny<Trip>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
