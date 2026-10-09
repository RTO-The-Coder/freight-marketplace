using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Fleet;
using Freight.Application.Offers;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Api.Tests.Integration;

/// <summary>
/// The whole offer flow over real HTTP against the test database: a company sees an open
/// shipment on its board, sends an offer, the shipper sees and accepts it, the company adds
/// it to its truck's trip - and the shipment moves Open -> Offered -> Approved -> off the board.
/// </summary>
public sealed class OfferFlowTests : ApiTestBase
{
    private static readonly DateTime ClockStart = new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

    [Fact]
    public async Task OpenShipment_GoesThroughOfferAcceptAndAddToTrip_OverHttp()
    {
        var (companyId, truckId, shipmentId) = await ArrangeAsync();

        var board = await BoardAsync(companyId);
        Assert.Contains(board.Open, s => s.ShipmentId == shipmentId);

        var sendResponse = await Client.PostAsJsonAsync($"/companies/{companyId}/shipments/{shipmentId}/offers", new
        {
            Offers = new[] { new { TruckId = truckId, PriceEur = 850.50m, LimitAt = (DateTime?)ClockStart.AddMinutes(30) } }
        }, JsonOptions);
        sendResponse.EnsureSuccessStatusCode();
        var offerId = Assert.Single((await sendResponse.Content.ReadFromJsonAsync<SendOffersResponse>(JsonOptions))!.OfferIds);

        board = await BoardAsync(companyId);
        Assert.DoesNotContain(board.Open, s => s.ShipmentId == shipmentId);
        var offered = Assert.Single(board.Offered, o => o.Shipment.ShipmentId == shipmentId);
        Assert.Equal("Truck-1", Assert.Single(offered.Offers).TruckName);

        var shipperView = await Client.GetFromJsonAsync<GetShipmentOffersResponse>($"/shipments/{shipmentId}/offers", JsonOptions);
        var shipperOffer = Assert.Single(shipperView!.Offers);
        Assert.Equal(offerId, shipperOffer.OfferId);
        Assert.Equal(850.50m, shipperOffer.PriceEur);
        Assert.Equal("Acme Trucking", shipperOffer.CompanyName);

        var acceptResponse = await Client.PostAsync($"/offers/{offerId}/accept", null);
        acceptResponse.EnsureSuccessStatusCode();

        board = await BoardAsync(companyId);
        Assert.Equal(offerId, Assert.Single(board.Approved, a => a.Shipment.ShipmentId == shipmentId).Offer.OfferId);

        var addResponse = await Client.PostAsync($"/offers/{offerId}/add-to-trip", null);
        addResponse.EnsureSuccessStatusCode();
        Assert.True((await addResponse.Content.ReadFromJsonAsync<AssignShipmentToTruckResponse>(JsonOptions))!.StopCount >= 2);

        board = await BoardAsync(companyId);
        Assert.DoesNotContain(board.Approved, a => a.Shipment.ShipmentId == shipmentId);
    }

    [Fact]
    public async Task SendOffers_ForTruckNotInCompany_Returns400()
    {
        var (companyId, _, shipmentId) = await ArrangeAsync();

        var response = await Client.PostAsJsonAsync($"/companies/{companyId}/shipments/{shipmentId}/offers", new
        {
            Offers = new[] { new { TruckId = Guid.NewGuid(), PriceEur = 850m, LimitAt = (DateTime?)null } }
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Board_UnknownCompany_Returns400()
    {
        var response = await Client.GetAsync($"/companies/{Guid.NewGuid()}/shipments/board");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Accept_UnknownOffer_Returns400()
    {
        var response = await Client.PostAsync($"/offers/{Guid.NewGuid()}/accept", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddToTrip_OfferNotAccepted_Returns400()
    {
        var (companyId, truckId, shipmentId) = await ArrangeAsync();
        var sendResponse = await Client.PostAsJsonAsync($"/companies/{companyId}/shipments/{shipmentId}/offers", new
        {
            Offers = new[] { new { TruckId = truckId, PriceEur = 850m, LimitAt = (DateTime?)null } }
        }, JsonOptions);
        var offerId = (await sendResponse.Content.ReadFromJsonAsync<SendOffersResponse>(JsonOptions))!.OfferIds[0];

        var response = await Client.PostAsync($"/offers/{offerId}/add-to-trip", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<GetCompanyShipmentBoardResponse> BoardAsync(Guid companyId) =>
        (await Client.GetFromJsonAsync<GetCompanyShipmentBoardResponse>($"/companies/{companyId}/shipments/board", JsonOptions))!;

    /// <summary>Sim clock at ClockStart, a company with one active Refrigerated truck + driver, and one open shipment.</summary>
    private async Task<(Guid CompanyId, Guid TruckId, Guid ShipmentId)> ArrangeAsync()
    {
        Factory.RoutingService.DefaultLeg = new RouteLeg(DistanceKm: 20, TimeTicks: 6);
        await Client.PostAsJsonAsync("/simulation/time", new { NewCurrentTime = ClockStart }, JsonOptions);

        var company = await Factory.SeedTruckingCompanyAsync();
        var truckResponse = await Client.PostAsJsonAsync("/trucks", new
        {
            TruckName = "Truck-1",
            TruckType = TruckType.Refrigerated,
            TruckSize = TruckSize.Medium
        }, JsonOptions);
        var truckId = (await truckResponse.Content.ReadFromJsonAsync<AddTruckResponse>(JsonOptions))!.TruckId;
        await Client.PostAsJsonAsync($"/trucks/{truckId}/company", new { TruckingCompanyId = company.Id }, JsonOptions);
        var driverResponse = await Client.PostAsJsonAsync("/drivers", new
        {
            FirstName = "Jane",
            LastName = "Doe",
            BreakRule = DrivingBreakRule.FullBreak,
            DailyRestRule = DailyRestRule.FullRest,
            WeeklyRestRule = WeeklyRestRule.FullWeeklyRest,
            ExtendDailyDrivingWhenEligible = false
        }, JsonOptions);
        var driverId = (await driverResponse.Content.ReadFromJsonAsync<AddDriverResponse>(JsonOptions))!.DriverId;
        await Client.PatchAsJsonAsync($"/trucks/{truckId}/drivers", new { PrimaryDriverId = driverId, SecondaryDriverId = (Guid?)null }, JsonOptions);
        await Client.PostAsync($"/trucks/{truckId}/activate", null);

        var shipper = await Factory.SeedShipperAsync();
        var bookResponse = await Client.PostAsJsonAsync("/shipments", new
        {
            ShipperId = shipper.Id,
            PickupLatitude = 52.52,
            PickupLongitude = 13.405,
            DeliveryLatitude = 48.1351,
            DeliveryLongitude = 11.582,
            LoadWeightKg = 50.0,
            LoadVolumeCubicMeters = 1.0,
            RequiredTruckType = TruckType.Refrigerated,
            PickupWindowEarliest = ClockStart,
            PickupWindowLatest = ClockStart.AddDays(2),
            DeliveryWindowEarliest = ClockStart,
            DeliveryWindowLatest = ClockStart.AddDays(3)
        }, JsonOptions);
        bookResponse.EnsureSuccessStatusCode();
        var shipmentId = (await bookResponse.Content.ReadFromJsonAsync<BookShipmentIdOnly>(JsonOptions))!.ShipmentId;

        return (company.Id, truckId, shipmentId);
    }

    private sealed record BookShipmentIdOnly(Guid ShipmentId);
}
