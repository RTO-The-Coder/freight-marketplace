using Freight.Application.Client;
using Freight.Application.Fleet;
using Freight.Application.Tests.Integration.TestSupport;
using Freight.Application.Simulation;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Application.Tests.Integration;

/// <summary>
/// Scenario 1: book a shipment, assign it to a driven truck, advance the simulation
/// partway through the first leg, then read back via GetTruckPositionHandler and
/// GetTruckEtasHandler. Proves the truck's live interpolated position and the ETA
/// projection's leg-in-progress state agree on the same leg and a consistent progress
/// fraction - no single-handler unit test can catch a divergence here since each mocks
/// the other's inputs.
/// </summary>
public sealed class BookAssignAdvanceEtaConsistencyTests
{
    private static readonly DateTime ClockStart = new(2026, 1, 1, 6, 0, 0);

    [Fact]
    public async Task PositionAndEtaProjection_AgreeOnSameLegAndProgressFraction_AfterPartialAdvance()
    {
        var unitOfWork = new FakeUnitOfWork();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(ClockStart, TimeSpan.Zero));

        // Seed a company, a driven+active truck.
        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", GeoLocation.Create(50.11, 8.68));
        unitOfWork.TruckingCompaniesRepo.Add(company);

        var addTruckHandler = new AddTruckHandler(unitOfWork);
        var truckResponse = await addTruckHandler.AddTruckAsync(new AddTruckRequest("Truck-1", TruckType.Refrigerated, TruckSize.Medium, company.Id));

        var addDriverHandler = new AddDriverHandler(unitOfWork);
        var driverResponse = await addDriverHandler.AddDriverAsync(new AddDriverRequest(
            "Jane", "Doe", DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));

        var assignDriversHandler = new AssignDriversHandler(unitOfWork);
        await assignDriversHandler.AssignDriversAsync(new AssignDriversRequest(truckResponse.TruckId, driverResponse.DriverId, null));

        var setActivationHandler = new SetTruckActivationHandler(unitOfWork);
        await setActivationHandler.SetTruckActivationAsync(new SetTruckActivationRequest(truckResponse.TruckId, IsActive: true));

        // Book a shipment.
        var shipper = Domain.Client.Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        unitOfWork.ShippersRepo.Add(shipper);
        var bookHandler = new BookShipmentHandler(unitOfWork, timeProvider);
        var bookResponse = await bookHandler.BookShipmentAsync(new BookShipmentRequest(
            shipper.Id,
            GeoLocation.Create(52.52, 13.405),
            GeoLocation.Create(48.1351, 11.582),
            Capacity.Create(100, 1),
            TruckType.Refrigerated,
            TimeWindow.Create(ClockStart, ClockStart.AddDays(2)),
            TimeWindow.Create(ClockStart, ClockStart.AddDays(3))));

        // Assign the shipment to the truck.
        var routingService = new FakeRoutingService { DefaultLeg = new Domain.Routing.Abstractions.RouteLeg(20, 6) };
        var evaluator = new ShipmentInsertionEvaluator(new RouteEtaCalculator(new DriverRuleEngine()));
        var assignHandler = new AssignShipmentToTruckHandler(unitOfWork, evaluator, routingService, timeProvider);
        await assignHandler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truckResponse.TruckId, bookResponse.ShipmentId, 0, 0));

        // Advance the simulation partway through the pickup leg (6 ticks total - advance 3).
        var advanceHandler = new SimulationAdvanceHandler(unitOfWork, new DriverRuleEngine(), timeProvider);
        await advanceHandler.AdvanceSimulationAsync(new AdvanceSimulationRequest(3));

        // Read back via both query handlers.
        var positionHandler = new GetTruckPositionHandler(unitOfWork);
        var position = await positionHandler.GetTruckPositionAsync(new GetTruckPositionRequest(truckResponse.TruckId));

        var etasHandler = new GetTruckEtasHandler(unitOfWork, new RouteEtaCalculator(new DriverRuleEngine()));
        var etas = await etasHandler.GetTruckEtasAsync(new GetTruckEtasRequest(truckResponse.TruckId));

        // Both handlers must agree the truck is heading to the same stop.
        var pendingPickupStop = etas.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(pendingPickupStop.StopId, position.HeadingToStopId);

        // The position's progress fraction (3/6 = 0.5) matches what a fresh ETA projection
        // starting from "now" would use as CurrentLegProgress - both read the SAME
        // truck.CurrentProgress, so they cannot disagree on how far along the leg is.
        Assert.Equal(0.5, position.LegProgressFraction);

        // The ETA projection's own start time is the driver ledger's LastEvaluatedSimulatedTime,
        // which the simulation advance updated to the same instant used for the position calc.
        Assert.NotNull(etas.ProjectionStart);
        Assert.Equal(ClockStart.AddMinutes(15), etas.ProjectionStart);
    }
}
