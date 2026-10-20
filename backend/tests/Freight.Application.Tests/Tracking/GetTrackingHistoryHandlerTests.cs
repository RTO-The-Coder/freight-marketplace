using Freight.Application.Fleet;
using Freight.Application.Offers;
using Freight.Application.Tests.Integration.TestSupport;
using Freight.Application.Tracking;

namespace Freight.Application.Tests.Tracking;

public sealed class GetTrackingHistoryHandlerTests
{
    private static readonly DateTime ClockStart = OfferScenario.ClockStart;

    /// <summary>
    /// One trip with one shipment through an accepted offer (€850): Office → P → D → Office,
    /// every leg 6 ticks / 20 km, so it completes 18 ticks (90 min) after the clock start.
    /// </summary>
    private static async Task<(OfferScenario World, Guid CompanyId, Guid TruckId, Guid ShipmentId)> CompletedTripAsync()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        var offerId = (await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, null))).OfferIds[0];
        await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(offerId));
        await world.AddToTrip().AddToTripAsync(new AddOfferToTripRequest(offerId));

        var advanced = await world.AdvanceSimulationAsync(18);
        Assert.Equal(1, advanced.TripsCompleted);

        return (world, companyId, truckId, shipmentId);
    }

    [Fact]
    public async Task GetHistoryAsync_TripCompletedInThePeriod_ListsItWithShipmentsAndSummary()
    {
        var (world, companyId, truckId, shipmentId) = await CompletedTripAsync();

        var response = await world.History().GetHistoryAsync(
            new GetTrackingHistoryRequest(companyId, ClockStart, ClockStart.AddDays(1), null));

        var trip = Assert.Single(response.Trips);
        Assert.Equal((truckId, "Truck-1"), (trip.TruckId, trip.TruckName));
        Assert.Equal(["Driver1 Doe"], trip.Drivers.Select(driver => driver.Name));
        Assert.Equal(ClockStart, trip.StartedAt);
        Assert.Equal(ClockStart.AddMinutes(90), trip.CompletedAt);
        Assert.Equal(60, trip.DistanceKm, precision: 6);
        Assert.Equal(18, trip.DrivingTicks);
        Assert.Equal(850m, trip.EarnedEur);

        var row = Assert.Single(trip.Shipments);
        Assert.Equal((shipmentId, TrackedShipmentState.Delivered, 0, 850m), (row.ShipmentId, row.State, row.LateByMinutes, row.PriceEur));
        Assert.Equal(ClockStart.AddMinutes(30), row.PickedUpAt);
        Assert.Equal(ClockStart.AddMinutes(60), row.DeliveredAt);

        var summary = response.Summary;
        Assert.Equal((1, 1, 850m, 100, 0), (summary.CompletedTrips, summary.ShipmentsDelivered, summary.EarnedEur, summary.OnTimePercent, summary.LateCount));
        Assert.Equal(0.05, summary.TonnesCarried, precision: 6);
        Assert.Equal(60, summary.DistanceKm, precision: 6);
    }

    [Fact]
    public async Task GetHistoryAsync_TripCompletedOutsideThePeriod_IsNotListed()
    {
        var (world, companyId, _, _) = await CompletedTripAsync();

        var before = await world.History().GetHistoryAsync(
            new GetTrackingHistoryRequest(companyId, ClockStart.AddDays(-7), ClockStart.AddMinutes(90), null));
        var after = await world.History().GetHistoryAsync(
            new GetTrackingHistoryRequest(companyId, ClockStart.AddMinutes(91), ClockStart.AddDays(7), null));

        Assert.Empty(before.Trips);
        Assert.Empty(after.Trips);
        Assert.Equal(0, before.Summary.CompletedTrips);
        Assert.Null(before.Summary.OnTimePercent);
    }

    [Fact]
    public async Task GetHistoryAsync_TruckFilter_ListsOnlyThatTrucksTrips()
    {
        var (world, companyId, truckId, _) = await CompletedTripAsync();
        var otherTruckId = await world.AddTruckAsync(companyId);

        var mine = await world.History().GetHistoryAsync(
            new GetTrackingHistoryRequest(companyId, ClockStart, ClockStart.AddDays(1), truckId));
        var other = await world.History().GetHistoryAsync(
            new GetTrackingHistoryRequest(companyId, ClockStart, ClockStart.AddDays(1), otherTruckId));

        Assert.Single(mine.Trips);
        Assert.Empty(other.Trips);
    }

    [Fact]
    public async Task GetHistoryAsync_RunningTrip_IsNotHistory()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync(directCompanyId: companyId);
        await world.AssignHandler().AssignShipmentAsync(new AssignShipmentToTruckRequest(truckId, shipmentId, 0, 0));
        await world.AdvanceSimulationAsync(6);

        var response = await world.History().GetHistoryAsync(
            new GetTrackingHistoryRequest(companyId, ClockStart, ClockStart.AddDays(1), null));

        Assert.Empty(response.Trips);
    }

    [Fact]
    public async Task GetHistoryAsync_EndNotAfterStart_Throws()
    {
        var (world, companyId, _, _) = await CompletedTripAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => world.History().GetHistoryAsync(
            new GetTrackingHistoryRequest(companyId, ClockStart, ClockStart, null)));
    }

    [Fact]
    public async Task SecondTripOnTheSameTruck_StartsWithCleanTotals()
    {
        // The first trip ended with its return-to-office leg complete on the truck; the
        // second trip must not count that leg as its own.
        var (world, companyId, truckId, _) = await CompletedTripAsync();

        var shipmentId = await world.BookOpenShipmentAsync(directCompanyId: companyId);
        await world.AssignHandler().AssignShipmentAsync(new AssignShipmentToTruckRequest(truckId, shipmentId, 0, 0));

        var secondTrip = await world.UnitOfWork.Trips.GetOpenTripByTruckIdAsync(truckId);
        Assert.NotNull(secondTrip);
        Assert.Equal(0, secondTrip.DistanceTravelledSoFar);
        Assert.Equal(0, secondTrip.TimeElapsedSoFar);
        Assert.Equal(["Driver1"], secondTrip.Drivers.Select(driver => driver.FirstName));
    }
}
