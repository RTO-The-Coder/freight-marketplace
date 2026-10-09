using Freight.Application.Offers;
using Freight.Application.Tests.Integration.TestSupport;
using Freight.Domain.Client.Enums;

namespace Freight.Application.Tests.Offers;

public sealed class AcceptOfferHandlerTests
{
    private static readonly DateTime ClockStart = OfferScenario.ClockStart;

    [Fact]
    public async Task AcceptOfferAsync_WaitingOffer_WinsRejectsAllOthersShipmentStaysPendingForWinner()
    {
        var world = new OfferScenario();
        var (companyA, truckA1) = await world.AddCompanyWithTruckAsync("A Trucking");
        var truckA2 = await world.AddTruckAsync(companyA);
        var (companyB, truckB) = await world.AddCompanyWithTruckAsync("B Trucking");
        var shipmentId = await world.BookOpenShipmentAsync();
        var sentA = await world.SendAsync(companyA, shipmentId, new OfferItem(truckA1, 850m, null), new OfferItem(truckA2, 900m, null));
        await world.SendAsync(companyB, shipmentId, new OfferItem(truckB, 800m, null));
        var winnerId = sentA.OfferIds[0];

        var response = await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(winnerId));

        Assert.Equal(shipmentId, response.ShipmentId);
        Assert.Equal(companyA, response.TruckingCompanyId);
        var offers = await world.UnitOfWork.ShipmentOffers.GetByShipmentIdAsync(shipmentId);
        Assert.Equal(ShipmentOfferStatus.Accepted, Assert.Single(offers, o => o.Id == winnerId).Status);
        Assert.All(offers.Where(o => o.Id != winnerId), o => Assert.Equal(ShipmentOfferStatus.Rejected, o.Status));

        var shipment = await world.UnitOfWork.Shipments.GetByIdAsync(shipmentId);
        Assert.Equal(ShipmentStatus.Pending, shipment!.Status);
        Assert.Equal(companyA, shipment.TruckingCompanyId);
        Assert.False(shipment.IsDirect);
    }

    [Fact]
    public async Task AcceptOfferAsync_AfterOffersOwnLimit_Throws_NothingChanges()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        var sent = await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, ClockStart.AddMinutes(10)));
        await world.AdvanceClockAsync(TimeSpan.FromMinutes(10));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Accept().AcceptOfferAsync(new AcceptOfferRequest(sent.OfferIds[0])));

        var shipment = await world.UnitOfWork.Shipments.GetByIdAsync(shipmentId);
        Assert.Null(shipment!.TruckingCompanyId);
    }

    [Fact]
    public async Task AcceptOfferAsync_AfterShipmentOfferDeadline_Throws()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        var sent = await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, null));
        await world.AdvanceClockAsync(TimeSpan.FromHours(2));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Accept().AcceptOfferAsync(new AcceptOfferRequest(sent.OfferIds[0])));
    }

    [Fact]
    public async Task AcceptOfferAsync_SecondAcceptOnSameShipment_Throws()
    {
        var world = new OfferScenario();
        var (companyA, truckA) = await world.AddCompanyWithTruckAsync("A Trucking");
        var (companyB, truckB) = await world.AddCompanyWithTruckAsync("B Trucking");
        var shipmentId = await world.BookOpenShipmentAsync();
        var sentA = await world.SendAsync(companyA, shipmentId, new OfferItem(truckA, 850m, null));
        var sentB = await world.SendAsync(companyB, shipmentId, new OfferItem(truckB, 800m, null));
        await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(sentA.OfferIds[0]));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Accept().AcceptOfferAsync(new AcceptOfferRequest(sentB.OfferIds[0])));
    }

    [Fact]
    public async Task AcceptOfferAsync_UnknownOffer_Throws()
    {
        var world = new OfferScenario();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.Accept().AcceptOfferAsync(new AcceptOfferRequest(Guid.NewGuid())));
    }
}
