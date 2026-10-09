using Freight.Application.Offers;
using Freight.Application.Tests.Integration.TestSupport;

namespace Freight.Application.Tests.Offers;

public sealed class GetCompanyShipmentBoardHandlerTests
{
    private static readonly DateTime ClockStart = OfferScenario.ClockStart;

    private static Task<GetCompanyShipmentBoardResponse> BoardFor(OfferScenario world, Guid companyId) =>
        world.Board().GetBoardAsync(new GetCompanyShipmentBoardRequest(companyId));

    private static void AssertOnlyIn(GetCompanyShipmentBoardResponse board, Guid shipmentId, string list)
    {
        Assert.Equal(list == "open", board.Open.Any(s => s.ShipmentId == shipmentId));
        Assert.Equal(list == "offered", board.Offered.Any(o => o.Shipment.ShipmentId == shipmentId));
        Assert.Equal(list == "approved", board.Approved.Any(a => a.Shipment.ShipmentId == shipmentId));
        Assert.Equal(list == "direct", board.Direct.Any(s => s.ShipmentId == shipmentId));
    }

    [Fact]
    public async Task GetBoardAsync_NewOpenShipment_InOpenOnly()
    {
        var world = new OfferScenario();
        var (companyId, _) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();

        AssertOnlyIn(await BoardFor(world, companyId), shipmentId, "open");
    }

    [Fact]
    public async Task GetBoardAsync_AfterOurOffers_InOfferedOnlyWithAllOurWaitingOffers()
    {
        var world = new OfferScenario();
        var (companyId, truckA) = await world.AddCompanyWithTruckAsync();
        var truckB = await world.AddTruckAsync(companyId);
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyId, shipmentId, new OfferItem(truckA, 900m, null), new OfferItem(truckB, 850m, null));

        var board = await BoardFor(world, companyId);

        AssertOnlyIn(board, shipmentId, "offered");
        var offered = Assert.Single(board.Offered);
        Assert.Equal(2, offered.Offers.Count);
        Assert.Equal(850m, offered.Offers[0].PriceEur);
        Assert.Equal("Truck-2", offered.Offers[0].TruckName);
    }

    [Fact]
    public async Task GetBoardAsync_OtherCompanysOffer_DoesNotMoveItOutOfOurOpenList()
    {
        var world = new OfferScenario();
        var (companyA, _) = await world.AddCompanyWithTruckAsync("A Trucking");
        var (companyB, truckB) = await world.AddCompanyWithTruckAsync("B Trucking");
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyB, shipmentId, new OfferItem(truckB, 800m, null));

        AssertOnlyIn(await BoardFor(world, companyA), shipmentId, "open");
    }

    [Fact]
    public async Task GetBoardAsync_OurOffersPastOwnLimit_InNoList()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, ClockStart.AddMinutes(10)));
        await world.AdvanceClockAsync(TimeSpan.FromMinutes(10));

        AssertOnlyIn(await BoardFor(world, companyId), shipmentId, "none");
    }

    [Fact]
    public async Task GetBoardAsync_AfterOfferDeadline_InNoList()
    {
        var world = new OfferScenario();
        var (companyId, _) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.AdvanceClockAsync(TimeSpan.FromHours(2));

        AssertOnlyIn(await BoardFor(world, companyId), shipmentId, "none");
    }

    [Fact]
    public async Task GetBoardAsync_OurOfferAccepted_InApprovedOnlyWithThatOffer_LoserSeesNothing()
    {
        var world = new OfferScenario();
        var (winner, truckW) = await world.AddCompanyWithTruckAsync("Winner Trucking");
        var (loser, truckL) = await world.AddCompanyWithTruckAsync("Loser Trucking");
        var shipmentId = await world.BookOpenShipmentAsync();
        var sent = await world.SendAsync(winner, shipmentId, new OfferItem(truckW, 850m, null));
        await world.SendAsync(loser, shipmentId, new OfferItem(truckL, 800m, null));
        await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(sent.OfferIds[0]));

        var winnerBoard = await BoardFor(world, winner);
        AssertOnlyIn(winnerBoard, shipmentId, "approved");
        var approved = Assert.Single(winnerBoard.Approved);
        Assert.Equal(sent.OfferIds[0], approved.Offer.OfferId);
        Assert.Equal(truckW, approved.Offer.TruckId);

        AssertOnlyIn(await BoardFor(world, loser), shipmentId, "none");
    }

    [Fact]
    public async Task GetBoardAsync_AfterAddToTrip_InNoList()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        var sent = await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, null));
        await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(sent.OfferIds[0]));
        await world.AddToTrip().AddToTripAsync(new AddOfferToTripRequest(sent.OfferIds[0]));

        AssertOnlyIn(await BoardFor(world, companyId), shipmentId, "none");
    }

    [Fact]
    public async Task GetBoardAsync_DirectShipment_InDirectForItsCompanyOnly_NeverExpires()
    {
        var world = new OfferScenario();
        var (companyId, _) = await world.AddCompanyWithTruckAsync("Chosen Trucking");
        var (otherId, _) = await world.AddCompanyWithTruckAsync("Other Trucking");
        var shipmentId = await world.BookOpenShipmentAsync(directCompanyId: companyId);
        await world.AdvanceClockAsync(TimeSpan.FromHours(5));

        var board = await BoardFor(world, companyId);
        AssertOnlyIn(board, shipmentId, "direct");
        Assert.True(Assert.Single(board.Direct).IsDirect);

        AssertOnlyIn(await BoardFor(world, otherId), shipmentId, "none");
    }

    [Fact]
    public async Task GetBoardAsync_UnknownCompany_Throws()
    {
        var world = new OfferScenario();

        await Assert.ThrowsAsync<InvalidOperationException>(() => BoardFor(world, Guid.NewGuid()));
    }
}
