using Freight.Application.Fleet;
using Freight.Application.Offers;
using Freight.Application.Tests.Integration.TestSupport;
using Freight.Application.Tracking;
using Freight.Domain.Client.Enums;

namespace Freight.Application.Tests.Tracking;

public sealed class GetCompanyTrackingHandlerTests
{
    private static readonly DateTime ClockStart = OfferScenario.ClockStart;

    /// <summary>
    /// One truck on a trip with three shipments - S1 through an accepted offer (€850), S2 and
    /// S3 booked direct - in the route P1 D1 P2 D2 P3 D3 Office, every leg 6 ticks / 20 km.
    /// Plus three more offers for the same truck: one waiting, one accepted but not yet added,
    /// one whose own limit runs out. After 18 ticks the truck has reached P1, D1 and P2.
    /// </summary>
    private static async Task<(OfferScenario World, Guid CompanyId, Guid TruckId, Guid[] OnTrip, Guid Waiting, Guid Accepted)> RunningTripAsync()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();

        var s1 = await world.BookOpenShipmentAsync();
        var s1Offer = (await world.SendAsync(companyId, s1, new OfferItem(truckId, 850m, null))).OfferIds[0];
        await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(s1Offer));
        await world.AddToTrip().AddToTripAsync(new AddOfferToTripRequest(s1Offer));

        var s2 = await world.BookOpenShipmentAsync(directCompanyId: companyId);
        await world.AssignHandler().AssignShipmentAsync(new AssignShipmentToTruckRequest(truckId, s2, 2, 2));
        var s3 = await world.BookOpenShipmentAsync(directCompanyId: companyId);
        await world.AssignHandler().AssignShipmentAsync(new AssignShipmentToTruckRequest(truckId, s3, 4, 4));

        var waiting = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyId, waiting, new OfferItem(truckId, 300m, null));

        var accepted = await world.BookOpenShipmentAsync();
        var acceptedOffer = (await world.SendAsync(companyId, accepted, new OfferItem(truckId, 250m, null))).OfferIds[0];
        await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(acceptedOffer));

        var expiring = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyId, expiring, new OfferItem(truckId, 200m, ClockStart.AddMinutes(10)));

        await world.AdvanceSimulationAsync(18);

        return (world, companyId, truckId, [s1, s2, s3], waiting, accepted);
    }

    [Fact]
    public async Task GetTrackingAsync_RunningTrip_ShowsEveryShipmentStateInRouteOrder_WithPrices()
    {
        var (world, companyId, _, onTrip, _, _) = await RunningTripAsync();

        var response = await world.Tracking().GetTrackingAsync(new GetCompanyTrackingRequest(companyId));

        var trip = Assert.Single(response.Trips);
        Assert.Collection(trip.Shipments,
            row =>
            {
                Assert.Equal((onTrip[0], TrackedShipmentState.Delivered, 850m), (row.ShipmentId, row.State, row.PriceEur));
                Assert.NotNull(row.PickedUpAt);
                Assert.NotNull(row.DeliveredAt);
                Assert.Null(row.Eta);
                Assert.Equal(0, row.LateByMinutes);
                Assert.Equal("Acme Shipping", row.ShipperName);
            },
            row =>
            {
                Assert.Equal((onTrip[1], TrackedShipmentState.OnBoard, (decimal?)null), (row.ShipmentId, row.State, row.PriceEur));
                Assert.NotNull(row.PickedUpAt);
                Assert.NotNull(row.Eta);
            },
            row =>
            {
                Assert.Equal((onTrip[2], TrackedShipmentState.ToPick, (decimal?)null), (row.ShipmentId, row.State, row.PriceEur));
                Assert.Null(row.PickedUpAt);
                Assert.NotNull(row.Eta);
            });
    }

    [Fact]
    public async Task GetTrackingAsync_RunningTrip_ListsTheTrucksLiveOffers_AcceptedFirst_ExpiredLeftOut()
    {
        var (world, companyId, _, _, waiting, accepted) = await RunningTripAsync();

        var response = await world.Tracking().GetTrackingAsync(new GetCompanyTrackingRequest(companyId));

        var trip = Assert.Single(response.Trips);
        Assert.Collection(trip.Offers,
            offer =>
            {
                Assert.Equal((accepted, ShipmentOfferStatus.Accepted, 250m), (offer.ShipmentId, offer.Status, offer.PriceEur));
                Assert.Null(offer.WaitingUntil);
            },
            offer =>
            {
                Assert.Equal((waiting, ShipmentOfferStatus.Pending, 300m), (offer.ShipmentId, offer.Status, offer.PriceEur));
                Assert.NotNull(offer.WaitingUntil);
            });
        Assert.Empty(response.ReadyTrucks);
    }

    [Fact]
    public async Task GetTrackingAsync_RunningTrip_TotalsIncludeTheLegsDriven_DriversAndNowLine()
    {
        var (world, companyId, truckId, _, _, _) = await RunningTripAsync();

        var response = await world.Tracking().GetTrackingAsync(new GetCompanyTrackingRequest(companyId));

        var trip = Assert.Single(response.Trips);
        Assert.Equal(truckId, trip.TruckId);
        Assert.Equal("Truck-1", trip.TruckName);
        Assert.Equal(["Driver1 Doe"], trip.Drivers.Select(driver => driver.Name));
        Assert.Equal(ClockStart, trip.StartedAt);
        Assert.Equal(60, trip.DistanceKmSoFar, precision: 6);
        Assert.Equal(18, trip.DrivingTicksSoFar);
        Assert.Equal(0, trip.RestTicksSoFar);
        Assert.Equal(3, trip.StopsDone);
        Assert.Equal(7, trip.Stops.Count);
        Assert.NotNull(trip.OfficeEta);

        Assert.Equal(TrackingNowKind.Driving, trip.Now.Kind);
        Assert.Equal("Pickup place", trip.Now.FromPlace);
        Assert.Equal("Delivery place", trip.Now.ToPlace);
        Assert.Equal(20, trip.Now.KmLeft, precision: 6);
        Assert.Equal("Driver1 Doe", trip.Now.DriverName);
    }

    [Fact]
    public async Task GetTrackingAsync_NotStartedYet_NowLineSaysWhenItStarts()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipment = await world.BookOpenShipmentAsync(directCompanyId: companyId);
        await world.AssignHandler().AssignShipmentAsync(
            new AssignShipmentToTruckRequest(truckId, shipment, 0, 0, TripStartTime: ClockStart.AddHours(3)));

        var response = await world.Tracking().GetTrackingAsync(new GetCompanyTrackingRequest(companyId));

        var trip = Assert.Single(response.Trips);
        Assert.Equal(TrackingNowKind.NotStarted, trip.Now.Kind);
        Assert.Equal(ClockStart.AddHours(3), trip.Now.Until);
        Assert.Equal("Office place", trip.Now.FromPlace);
    }

    [Fact]
    public async Task GetTrackingAsync_IdleTruckWithAnAcceptedOffer_IsReadyToStart_AndHasNoCard()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var idleTruckId = await world.AddTruckAsync(companyId);

        var shipment = await world.BookOpenShipmentAsync();
        var offerId = (await world.SendAsync(companyId, shipment, new OfferItem(idleTruckId, 280m, null))).OfferIds[0];
        await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(offerId));

        // The first truck, idle too, has only a waiting offer - not ready.
        var waiting = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyId, waiting, new OfferItem(truckId, 300m, null));

        var response = await world.Tracking().GetTrackingAsync(new GetCompanyTrackingRequest(companyId));

        Assert.Empty(response.Trips);
        var ready = Assert.Single(response.ReadyTrucks);
        Assert.Equal(idleTruckId, ready.TruckId);
        Assert.Equal(["Driver2 Doe"], ready.Drivers.Select(driver => driver.Name));
        var offer = Assert.Single(ready.AcceptedOffers);
        Assert.Equal((offerId, shipment, 280m), (offer.OfferId, offer.ShipmentId, offer.PriceEur));
    }

    [Fact]
    public async Task GetTrackingAsync_OtherCompanysTrips_AreNotShown()
    {
        var world = new OfferScenario();
        var (companyId, _) = await world.AddCompanyWithTruckAsync();
        var (otherCompanyId, otherTruckId) = await world.AddCompanyWithTruckAsync("Other Trucking");
        var shipment = await world.BookOpenShipmentAsync(directCompanyId: otherCompanyId);
        await world.AssignHandler().AssignShipmentAsync(new AssignShipmentToTruckRequest(otherTruckId, shipment, 0, 0));

        var response = await world.Tracking().GetTrackingAsync(new GetCompanyTrackingRequest(companyId));

        Assert.Empty(response.Trips);
    }

    [Fact]
    public async Task GetTrackingAsync_UnknownCompany_Throws()
    {
        var world = new OfferScenario();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Tracking().GetTrackingAsync(new GetCompanyTrackingRequest(Guid.NewGuid())));
    }
}
