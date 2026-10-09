using Freight.Application.Offers;
using Freight.Application.Tests.Integration.TestSupport;
using Freight.Domain.Client.Enums;

namespace Freight.Application.Tests.Offers;

public sealed class AddOfferToTripHandlerTests
{
    private static async Task<(OfferScenario World, Guid ShipmentId, Guid OfferId, Guid TruckId)> AcceptedOfferAsync()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        var sent = await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, null));
        await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(sent.OfferIds[0]));
        return (world, shipmentId, sent.OfferIds[0], truckId);
    }

    [Fact]
    public async Task AddToTripAsync_AcceptedOffer_AddsStopsToOfferedTruckAndBooksShipment()
    {
        var (world, shipmentId, offerId, truckId) = await AcceptedOfferAsync();

        var response = await world.AddToTrip().AddToTripAsync(new AddOfferToTripRequest(offerId));

        Assert.True(response.StopCount >= 2);
        var trip = await world.UnitOfWork.Trips.GetOpenTripByTruckIdAsync(truckId);
        Assert.NotNull(trip);
        Assert.Equal(2, trip.Stops.Count(stop => stop.ShipmentId == shipmentId));
        var shipment = await world.UnitOfWork.Shipments.GetByIdAsync(shipmentId);
        Assert.Equal(ShipmentStatus.Booked, shipment!.Status);
    }

    [Fact]
    public async Task AddToTripAsync_ShipmentAlreadyOnTrip_Throws()
    {
        var (world, _, offerId, _) = await AcceptedOfferAsync();
        await world.AddToTrip().AddToTripAsync(new AddOfferToTripRequest(offerId));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.AddToTrip().AddToTripAsync(new AddOfferToTripRequest(offerId)));

        Assert.Contains("already been added", ex.Message);
    }

    [Fact]
    public async Task AddToTripAsync_OfferNotAccepted_Throws_NoTripCreated()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        var sent = await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.AddToTrip().AddToTripAsync(new AddOfferToTripRequest(sent.OfferIds[0])));

        Assert.Contains("accepted", ex.Message);
        Assert.Null(await world.UnitOfWork.Trips.GetOpenTripByTruckIdAsync(truckId));
    }

    [Fact]
    public async Task AddToTripAsync_UnknownOffer_Throws()
    {
        var world = new OfferScenario();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.AddToTrip().AddToTripAsync(new AddOfferToTripRequest(Guid.NewGuid())));
    }

    [Fact]
    public async Task DirectAssign_AcceptedShipmentToOtherCompanysTruck_Throws()
    {
        var (world, shipmentId, _, _) = await AcceptedOfferAsync();
        var (_, otherTruckId) = await world.AddCompanyWithTruckAsync("Other Trucking");

        await Assert.ThrowsAsync<InvalidOperationException>(() => world.AssignHandler().AssignShipmentAsync(
            new Freight.Application.Fleet.AssignShipmentToTruckRequest(otherTruckId, shipmentId, 0, 0)));

        var shipment = await world.UnitOfWork.Shipments.GetByIdAsync(shipmentId);
        Assert.Equal(ShipmentStatus.Pending, shipment!.Status);
    }
}
