using Freight.Application.Client;
using Freight.Application.Fleet;
using Freight.Application.Tests.Integration.TestSupport;
using Freight.Domain.Client.Enums;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Fleet.Services;
using Freight.Domain.Tracking.Services;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Application.Tests.Integration;

/// <summary>
/// Scenario 2: book two shipments, confirm both appear as Pending, assign ONE to a truck
/// (which moves it out of Pending via Shipment.AssignToCompany), and confirm it vanishes
/// from the pending list but still appears in the shipper's full history with the updated
/// status. Proves GetPendingShipmentsHandler's status filter and
/// AssignShipmentToTruckHandler's status-mutating side effect stay in lockstep - a unit
/// test that mocks the repo's returned list directly cannot verify this.
/// </summary>
public sealed class PendingShipmentDisappearsAfterBookingTests
{
    private static readonly DateTime ClockStart = new(2026, 1, 1, 6, 0, 0);

    [Fact]
    public async Task AssignedShipment_VanishesFromPendingList_ButRemainsInShipperHistoryWithUpdatedStatus()
    {
        var unitOfWork = new FakeUnitOfWork();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(ClockStart, TimeSpan.Zero));

        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", GeoLocation.Create(50.11, 8.68));
        unitOfWork.TruckingCompaniesRepo.Add(company);
        var truckResponse = await new AddTruckHandler(unitOfWork).AddTruckAsync(
            new AddTruckRequest("Truck-1", TruckType.Refrigerated, TruckSize.Medium, company.Id));
        var driverResponse = await new AddDriverHandler(unitOfWork).AddDriverAsync(
            new AddDriverRequest("Jane", "Doe", DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));
        await new AssignDriversHandler(unitOfWork).AssignDriversAsync(new AssignDriversRequest(truckResponse.TruckId, driverResponse.DriverId, null));
        await new SetTruckActivationHandler(unitOfWork).SetTruckActivationAsync(new SetTruckActivationRequest(truckResponse.TruckId, true));

        var shipper = Domain.Client.Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        unitOfWork.ShippersRepo.Add(shipper);
        var bookHandler = new BookShipmentHandler(unitOfWork, timeProvider);

        var shipmentA = await bookHandler.BookShipmentAsync(new BookShipmentRequest(
            shipper.Id, GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
            Capacity.Create(50, 1), TruckType.Refrigerated,
            TimeWindow.Create(ClockStart, ClockStart.AddDays(2)), TimeWindow.Create(ClockStart, ClockStart.AddDays(3))));
        var shipmentB = await bookHandler.BookShipmentAsync(new BookShipmentRequest(
            shipper.Id, GeoLocation.Create(52.0, 13.0), GeoLocation.Create(48.0, 11.0),
            Capacity.Create(50, 1), TruckType.Refrigerated,
            TimeWindow.Create(ClockStart, ClockStart.AddDays(2)), TimeWindow.Create(ClockStart, ClockStart.AddDays(3))));

        var pendingHandler = new GetPendingShipmentsHandler(unitOfWork);
        var pendingBefore = await pendingHandler.GetPendingShipmentsAsync();
        Assert.Equal(2, pendingBefore.Shipments.Count);

        // Assign shipment A to the truck - moves it Pending -> Booked.
        var routingService = new FakeRoutingService { DefaultLeg = new Domain.Routing.Abstractions.RouteLeg(20, 6) };
        var evaluator = new ShipmentInsertionEvaluator(new RouteEtaCalculator(new DriverRuleEngine()));
        var assignHandler = new AssignShipmentToTruckHandler(unitOfWork, evaluator, routingService, timeProvider);
        await assignHandler.AssignShipmentAsync(new AssignShipmentToTruckRequest(truckResponse.TruckId, shipmentA.ShipmentId, 0, 0));

        var pendingAfter = await pendingHandler.GetPendingShipmentsAsync();
        var byShipperHandler = new GetShipmentsByShipperHandler(unitOfWork);
        var shipperHistory = await byShipperHandler.GetShipmentsByShipperAsync(new GetShipmentsByShipperRequest(shipper.Id));

        // Shipment A is gone from the pending list; shipment B remains.
        Assert.Single(pendingAfter.Shipments);
        Assert.Equal(shipmentB.ShipmentId, pendingAfter.Shipments[0].ShipmentId);

        // Shipment A still appears in the shipper's full history, now Booked.
        Assert.Equal(2, shipperHistory.Shipments.Count);
        var shipmentADto = shipperHistory.Shipments.First(s => s.ShipmentId == shipmentA.ShipmentId);
        Assert.Equal(ShipmentStatus.Booked, shipmentADto.Status);
        Assert.Equal(company.Id, shipmentADto.TruckingCompanyId);
    }
}
