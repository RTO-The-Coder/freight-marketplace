using Freight.Application.Client;
using Freight.Application.Fleet;
using Freight.Application.Simulation;
using Freight.Application.Tests.Integration.TestSupport;
using Freight.Application.Tracking;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Application.Tests.Integration;

/// <summary>
/// Scenario 5: book + assign a shipment onto a not-yet-departed trip, reschedule it
/// earlier via RescheduleTripHandler, then advance the simulation clock across the new
/// start time. Confirms SimulationAdvanceHandler's own "has this trip started yet"
/// skip-tick guard (trip.StartedAt >= tickNow) actually honors the rescheduled time - not
/// the original one - catching a drift between the reschedule mutation and how the tick
/// loop independently interprets "has this trip started."
/// </summary>
public sealed class RescheduleAndEligibilityConsistencyTests
{
    private static readonly DateTime ClockStart = new(2026, 1, 1, 6, 0, 0);
    private static readonly GeoLocation Office = GeoLocation.Create(50.11, 8.68);

    [Fact]
    public async Task RescheduledEarlierStart_SimulationAdvanceHonorsNewStartTimeNotOriginal()
    {
        var unitOfWork = new FakeUnitOfWork();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(ClockStart, TimeSpan.Zero));

        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", Office);
        unitOfWork.TruckingCompaniesRepo.Add(company);
        var truckResponse = await new AddTruckHandler(unitOfWork).AddTruckAsync(
            new AddTruckRequest("Truck-1", TruckType.Refrigerated, TruckSize.Medium, company.Id));
        var driverResponse = await new AddDriverHandler(unitOfWork).AddDriverAsync(
            new AddDriverRequest("Jane", "Doe", DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));
        await new AssignDriversHandler(unitOfWork).AssignDriversAsync(new AssignDriversRequest(truckResponse.TruckId, driverResponse.DriverId, null));
        await new SetTruckActivationHandler(unitOfWork).SetTruckActivationAsync(new SetTruckActivationRequest(truckResponse.TruckId, true));

        var shipper = Domain.Client.Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        unitOfWork.ShippersRepo.Add(shipper);
        var bookResponse = await new BookShipmentHandler(unitOfWork, timeProvider).BookShipmentAsync(new BookShipmentRequest(
            shipper.Id, GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
            Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(ClockStart, ClockStart.AddDays(2)), TimeWindow.Create(ClockStart, ClockStart.AddDays(3))));

        // Open the trip with a planned start FAR in the future - the truck must not move at all initially.
        var farFutureStart = ClockStart.AddHours(2);
        var routingService = new FakeRoutingService { DefaultLeg = new Domain.Routing.Abstractions.RouteLeg(20, 6) };
        var evaluator = new ShipmentInsertionEvaluator(new RouteEtaCalculator(new DriverRuleEngine()));
        var assignHandler = new AssignShipmentToTruckHandler(unitOfWork, evaluator, routingService, timeProvider);
        await assignHandler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truckResponse.TruckId, bookResponse.ShipmentId, 0, 0, farFutureStart));

        var trip = (await unitOfWork.TripsRepo.GetOpenTripByTruckIdAsync(truckResponse.TruckId))!;
        Assert.Equal(farFutureStart, trip.StartedAt);

        // Reschedule it to start almost immediately (2 ticks after ClockStart).
        var newStart = ClockStart.AddMinutes(10);
        var rescheduleHandler = new RescheduleTripHandler(unitOfWork);
        await rescheduleHandler.RescheduleTripAsync(new RescheduleTripRequest(trip.Id, newStart));
        Assert.Equal(newStart, trip.StartedAt);

        // Advance 5 ticks (25 min) from ClockStart - if the tick loop still honored the
        // ORIGINAL far-future start, nothing would move; it must honor the RESCHEDULED
        // (earlier) start instead, so driving begins from tick 3 onward (3 driving ticks).
        var advanceHandler = new SimulationAdvanceHandler(unitOfWork, new DriverRuleEngine(), timeProvider);
        await advanceHandler.AdvanceSimulationAsync(new AdvanceSimulationRequest(5));

        var truck = await unitOfWork.TrucksRepo.GetByIdAsync(truckResponse.TruckId);
        Assert.True(truck!.CurrentProgress!.CurrentDrivingTimeTick > 0, "Expected the truck to have started driving after the rescheduled start time passed.");

        // CheckDriverEligibilityHandler's read-only probe must reflect the SAME ledger state
        // the tick loop actually produced - not a stale pre-reschedule snapshot.
        var eligibilityHandler = new CheckDriverEligibilityHandler(unitOfWork, new DriverRuleEngine());
        var eligibility = await eligibilityHandler.CheckDriverEligibilityAsync(new CheckDriverEligibilityRequest(driverResponse.DriverId, AfterMinutes: 0));
        Assert.True(eligibility.IsEligible);
    }
}
