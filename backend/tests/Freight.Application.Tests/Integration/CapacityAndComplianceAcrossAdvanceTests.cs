using Freight.Application.Client;
using Freight.Application.Fleet;
using Freight.Application.Simulation;
using Freight.Application.Tests.Integration.TestSupport;
using Freight.Application.Tracking;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Fleet.ValueObjects;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Application.Tests.Integration;

/// <summary>
/// Scenario 3: two shipments on the same truck/trip, sequenced so the first is delivered
/// (freeing capacity) before the second is picked up. Advances the simulation tick by
/// tick through both pickups and the first delivery, checking that Trip.CurrentLoad (via
/// GetTruckDetailHandler) and the driver's compliance ledger (via GetDriverDetailHandler)
/// stay consistent with the truck's actual physical progress (via
/// GetTruckPositionHandler) at each stage - proving the tick-loop's ledger updates and
/// route-progress updates never drift apart even though they are separate aggregate calls
/// per tick.
/// </summary>
public sealed class CapacityAndComplianceAcrossAdvanceTests
{
    private static readonly DateTime ClockStart = new(2026, 1, 1, 6, 0, 0);
    private static readonly GeoLocation Office = GeoLocation.Create(50.11, 8.68);

    [Fact]
    public async Task SequencedShipments_CapacityAndComplianceStayConsistentAcrossTicks()
    {
        var unitOfWork = new FakeUnitOfWork();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(ClockStart, TimeSpan.Zero));

        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", Office);
        unitOfWork.TruckingCompaniesRepo.Add(company);
        // 60kg load, Medium truck capacity 9000kg/45m3 - plenty of room, capacity isn't the
        // binding constraint here; this exercises SEQUENCING (pickup1 -> delivery1 -> pickup2)
        // rather than an actual violation.
        var truckResponse = await new AddTruckHandler(unitOfWork).AddTruckAsync(
            new AddTruckRequest("Truck-1", TruckType.Refrigerated, TruckSize.Medium, company.Id));
        var driverResponse = await new AddDriverHandler(unitOfWork).AddDriverAsync(
            new AddDriverRequest("Jane", "Doe", DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));
        await new AssignDriversHandler(unitOfWork).AssignDriversAsync(new AssignDriversRequest(truckResponse.TruckId, driverResponse.DriverId, null));
        await new SetTruckActivationHandler(unitOfWork).SetTruckActivationAsync(new SetTruckActivationRequest(truckResponse.TruckId, true));

        var shipper = Domain.Client.Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        unitOfWork.ShippersRepo.Add(shipper);
        var bookHandler = new BookShipmentHandler(unitOfWork, timeProvider);
        var pickupA = GeoLocation.Create(52.52, 13.405);
        var deliveryA = GeoLocation.Create(51.0, 12.0);
        var pickupB = GeoLocation.Create(50.5, 11.5);
        var deliveryB = GeoLocation.Create(48.1351, 11.582);

        var shipmentA = await bookHandler.BookShipmentAsync(new BookShipmentRequest(
            shipper.Id, pickupA, deliveryA, Capacity.Create(60, 1), TruckType.Refrigerated,
            TimeWindow.Create(ClockStart, ClockStart.AddDays(2)), TimeWindow.Create(ClockStart, ClockStart.AddDays(3))));

        var routingService = new FakeRoutingService { DefaultLeg = new Domain.Routing.Abstractions.RouteLeg(20, 2) };
        var evaluator = new ShipmentInsertionEvaluator(new RouteEtaCalculator(new DriverRuleEngine()));
        var assignHandler = new AssignShipmentToTruckHandler(unitOfWork, evaluator, routingService, timeProvider);
        await assignHandler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truckResponse.TruckId, shipmentA.ShipmentId, 0, 0));

        var shipmentB = await bookHandler.BookShipmentAsync(new BookShipmentRequest(
            shipper.Id, pickupB, deliveryB, Capacity.Create(60, 1), TruckType.Refrigerated,
            TimeWindow.Create(ClockStart, ClockStart.AddDays(2)), TimeWindow.Create(ClockStart, ClockStart.AddDays(3))));
        // Append shipment B's pickup+delivery AFTER shipment A's delivery (index 2), so A
        // is fully delivered before B is picked up - both legs 2 ticks each.
        await assignHandler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truckResponse.TruckId, shipmentB.ShipmentId, 2, 2));

        var advanceHandler = new SimulationAdvanceHandler(unitOfWork, new DriverRuleEngine(), timeProvider);
        var truckDetailHandler = new GetTruckDetailHandler(unitOfWork);
        var driverDetailHandler = new GetDriverDetailHandler(unitOfWork);
        var positionHandler = new GetTruckPositionHandler(unitOfWork);

        // Tick 1-2: drive to and reach shipment A's pickup.
        await advanceHandler.AdvanceSimulationAsync(new AdvanceSimulationRequest(2));
        var detailAfterPickupA = await truckDetailHandler.GetTruckDetailAsync(new GetTruckDetailRequest(truckResponse.TruckId));
        Assert.Equal(StopStatus.Reached, detailAfterPickupA.Stops.First(s => s.StopId == GetStopId(detailAfterPickupA, shipmentA.ShipmentId, StopKind.Pickup)).Status);

        // Tick 3-4: drive to and reach shipment A's delivery (capacity freed).
        await advanceHandler.AdvanceSimulationAsync(new AdvanceSimulationRequest(2));
        var detailAfterDeliveryA = await truckDetailHandler.GetTruckDetailAsync(new GetTruckDetailRequest(truckResponse.TruckId));
        Assert.Equal(StopStatus.Reached, detailAfterDeliveryA.Stops.First(s => s.StopId == GetStopId(detailAfterDeliveryA, shipmentA.ShipmentId, StopKind.Delivery)).Status);

        var driverDetail = await driverDetailHandler.GetDriverDetailAsync(new GetDriverDetailRequest(driverResponse.DriverId));
        var position = await positionHandler.GetTruckPositionAsync(new GetTruckPositionRequest(truckResponse.TruckId));

        // 4 driving ticks so far = 20 minutes of driving accrued on the ledger.
        Assert.Equal(20, driverDetail.ComplianceState!.DailyDrivingMinutesToday);
        // The truck is now heading to shipment B's pickup - position and detail agree.
        var pendingPickupB = detailAfterDeliveryA.Stops.First(s => s.Kind == StopKind.Pickup && s.Status == StopStatus.Pending);
        Assert.Equal(pendingPickupB.StopId, position.HeadingToStopId);
    }

    private static Guid GetStopId(TruckDetailDto detail, Guid shipmentId, StopKind kind) =>
        detail.Stops.First(s => s.ShipmentId == shipmentId && s.Kind == kind).StopId;
}
