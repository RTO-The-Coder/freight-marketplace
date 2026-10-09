using Freight.Application.Offers;
using Freight.Application.Tests.Integration.TestSupport;

namespace Freight.Application.Tests.Offers;

public sealed class GetShipmentOffersHandlerTests
{
    private static readonly DateTime ClockStart = OfferScenario.ClockStart;

    private static Task<GetShipmentOffersResponse> OffersFor(OfferScenario world, Guid shipmentId) =>
        world.ShipmentOffers().GetOffersAsync(new GetShipmentOffersRequest(shipmentId));

    [Fact]
    public async Task GetOffersAsync_WaitingOffers_CheapestFirstWithCompanyNamesAndDeadline()
    {
        var world = new OfferScenario();
        var (companyA, truckA) = await world.AddCompanyWithTruckAsync("A Trucking");
        var (companyB, truckB) = await world.AddCompanyWithTruckAsync("B Trucking");
        var shipmentId = await world.BookOpenShipmentAsync();
        var limit = ClockStart.AddMinutes(30);
        await world.SendAsync(companyA, shipmentId, new OfferItem(truckA, 900m, limit));
        await world.SendAsync(companyB, shipmentId, new OfferItem(truckB, 850m, null));

        var response = await OffersFor(world, shipmentId);

        Assert.Equal(shipmentId, response.ShipmentId);
        Assert.Equal(ClockStart.AddHours(2), response.OfferDeadline);
        Assert.Collection(response.Offers,
            first =>
            {
                Assert.Equal("B Trucking", first.CompanyName);
                Assert.Equal(companyB, first.TruckingCompanyId);
                Assert.Equal(850m, first.PriceEur);
                Assert.Null(first.LimitAt);
            },
            second =>
            {
                Assert.Equal("A Trucking", second.CompanyName);
                Assert.Equal(900m, second.PriceEur);
                Assert.Equal(limit, second.LimitAt);
            });
    }

    [Fact]
    public async Task GetOffersAsync_OfferPastOwnLimit_Hidden()
    {
        var world = new OfferScenario();
        var (companyA, truckA) = await world.AddCompanyWithTruckAsync("A Trucking");
        var (companyB, truckB) = await world.AddCompanyWithTruckAsync("B Trucking");
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyA, shipmentId, new OfferItem(truckA, 900m, ClockStart.AddMinutes(10)));
        await world.SendAsync(companyB, shipmentId, new OfferItem(truckB, 850m, null));
        await world.AdvanceClockAsync(TimeSpan.FromMinutes(10));

        var response = await OffersFor(world, shipmentId);

        Assert.Equal("B Trucking", Assert.Single(response.Offers).CompanyName);
    }

    [Fact]
    public async Task GetOffersAsync_AfterOfferDeadline_Empty()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, null));
        await world.AdvanceClockAsync(TimeSpan.FromHours(2));

        Assert.Empty((await OffersFor(world, shipmentId)).Offers);
    }

    [Fact]
    public async Task GetOffersAsync_AfterAccept_RejectedAndAcceptedBothHidden()
    {
        var world = new OfferScenario();
        var (companyA, truckA) = await world.AddCompanyWithTruckAsync("A Trucking");
        var (companyB, truckB) = await world.AddCompanyWithTruckAsync("B Trucking");
        var shipmentId = await world.BookOpenShipmentAsync();
        var sentA = await world.SendAsync(companyA, shipmentId, new OfferItem(truckA, 900m, null));
        await world.SendAsync(companyB, shipmentId, new OfferItem(truckB, 850m, null));
        await world.Accept().AcceptOfferAsync(new AcceptOfferRequest(sentA.OfferIds[0]));

        Assert.Empty((await OffersFor(world, shipmentId)).Offers);
    }

    [Fact]
    public async Task GetOffersAsync_UnknownShipment_Throws()
    {
        var world = new OfferScenario();

        await Assert.ThrowsAsync<InvalidOperationException>(() => OffersFor(world, Guid.NewGuid()));
    }
}
