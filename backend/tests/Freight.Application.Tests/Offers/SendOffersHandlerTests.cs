using Freight.Application.Offers;
using Freight.Application.Tests.Integration.TestSupport;
using Freight.Domain.Client.Enums;

namespace Freight.Application.Tests.Offers;

public sealed class SendOffersHandlerTests
{
    private static readonly DateTime ClockStart = OfferScenario.ClockStart;

    [Fact]
    public async Task SendOffersAsync_FeasibleTrucks_CreatesOnePendingOfferPerTruckWithEvaluatedPositions()
    {
        var world = new OfferScenario();
        var (companyId, truckA) = await world.AddCompanyWithTruckAsync();
        var truckB = await world.AddTruckAsync(companyId);
        var shipmentId = await world.BookOpenShipmentAsync();
        var limit = ClockStart.AddMinutes(10);

        var response = await world.SendAsync(companyId, shipmentId,
            new OfferItem(truckA, 850m, limit),
            new OfferItem(truckB, 910m, null));

        Assert.Equal(2, response.OfferIds.Count);
        var offers = await world.UnitOfWork.ShipmentOffers.GetByShipmentIdAsync(shipmentId);
        var offerA = Assert.Single(offers, o => o.TruckId == truckA);
        Assert.Equal(companyId, offerA.TruckingCompanyId);
        Assert.Equal(850m, offerA.PriceEur);
        Assert.Equal(limit, offerA.LimitAt);
        Assert.Equal(ClockStart, offerA.CreatedAt);
        Assert.Equal(ShipmentOfferStatus.Pending, offerA.Status);
        Assert.True(offerA.PickupInsertIndex >= 0);
        Assert.True(offerA.DeliveryInsertIndex >= offerA.PickupInsertIndex);
        Assert.True(offerA.AddedDistanceKm >= 0);
        Assert.Null(Assert.Single(offers, o => o.TruckId == truckB).LimitAt);
    }

    [Fact]
    public async Task SendOffersAsync_AfterOfferDeadline_Throws()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.AdvanceClockAsync(TimeSpan.FromHours(2));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, null)));

        Assert.Contains("closed", ex.Message);
    }

    [Fact]
    public async Task SendOffersAsync_DirectShipment_Throws()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync(directCompanyId: companyId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, null)));
    }

    [Fact]
    public async Task SendOffersAsync_SecondRoundFromSameCompany_Throws_EvenWithAnotherTruck()
    {
        var world = new OfferScenario();
        var (companyId, truckA) = await world.AddCompanyWithTruckAsync();
        var truckB = await world.AddTruckAsync(companyId);
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyId, shipmentId, new OfferItem(truckA, 850m, null));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.SendAsync(companyId, shipmentId, new OfferItem(truckB, 900m, null)));

        Assert.Contains("already sent", ex.Message);
    }

    [Fact]
    public async Task SendOffersAsync_SecondRoundAfterOwnLimitPassed_StillThrows()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, ClockStart.AddMinutes(10)));
        await world.AdvanceClockAsync(TimeSpan.FromMinutes(20));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 800m, null)));
    }

    [Fact]
    public async Task SendOffersAsync_OtherCompanyCanStillOffer()
    {
        var world = new OfferScenario();
        var (companyA, truckA) = await world.AddCompanyWithTruckAsync("A Trucking");
        var (companyB, truckB) = await world.AddCompanyWithTruckAsync("B Trucking");
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyA, shipmentId, new OfferItem(truckA, 850m, null));

        var response = await world.SendAsync(companyB, shipmentId, new OfferItem(truckB, 800m, null));

        Assert.Single(response.OfferIds);
    }

    [Fact]
    public async Task SendOffersAsync_TruckFromAnotherCompany_Throws_NothingSaved()
    {
        var world = new OfferScenario();
        var (companyA, _) = await world.AddCompanyWithTruckAsync("A Trucking");
        var (_, truckOfB) = await world.AddCompanyWithTruckAsync("B Trucking");
        var shipmentId = await world.BookOpenShipmentAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.SendAsync(companyA, shipmentId, new OfferItem(truckOfB, 850m, null)));

        Assert.Contains("does not belong", ex.Message);
        Assert.Empty(await world.UnitOfWork.ShipmentOffers.GetByShipmentIdAsync(shipmentId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(121)]
    public async Task SendOffersAsync_LimitNotAfterNowOrPastDeadline_Throws(int limitMinutesFromStart)
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => world.SendAsync(
            companyId, shipmentId, new OfferItem(truckId, 850m, ClockStart.AddMinutes(limitMinutesFromStart))));
    }

    [Fact]
    public async Task SendOffersAsync_LimitExactlyAtDeadline_IsAllowed()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();

        var response = await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, ClockStart.AddHours(2)));

        Assert.Single(response.OfferIds);
    }

    [Fact]
    public async Task SendOffersAsync_PriceNotAboveZero_Throws()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 0m, null)));
    }

    [Fact]
    public async Task SendOffersAsync_NoTrucks_Throws()
    {
        var world = new OfferScenario();
        var (companyId, _) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => world.SendAsync(companyId, shipmentId));
    }

    [Fact]
    public async Task SendOffersAsync_SameTruckTwice_Throws()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => world.SendAsync(companyId, shipmentId,
            new OfferItem(truckId, 850m, null), new OfferItem(truckId, 900m, null)));
    }

    [Fact]
    public async Task SendOffersAsync_AfterShipperChangesTimes_SameTruckCanOfferAgain()
    {
        var world = new OfferScenario();
        var (companyId, truckId) = await world.AddCompanyWithTruckAsync();
        var shipmentId = await world.BookOpenShipmentAsync();
        await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 850m, null));

        await new Freight.Application.Client.UpdateShipmentWindowsHandler(world.UnitOfWork, world.TimeProvider, world.Sender.Object)
            .HandleAsync(new Freight.Application.Client.UpdateShipmentWindowsRequest(
                shipmentId,
                Domain.ValueObjects.TimeWindow.Create(ClockStart, ClockStart.AddDays(2)),
                Domain.ValueObjects.TimeWindow.Create(ClockStart, ClockStart.AddDays(3))));

        var response = await world.SendAsync(companyId, shipmentId, new OfferItem(truckId, 800m, null));

        Assert.Single(response.OfferIds);
    }
}
