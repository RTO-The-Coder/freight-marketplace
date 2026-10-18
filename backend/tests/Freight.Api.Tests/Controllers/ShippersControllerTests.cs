using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Client;

namespace Freight.Api.Tests.Controllers;

public sealed class ShippersControllerTests : ApiTestBase
{
    [Fact]
    public async Task GetShippers_NoneExist_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/shippers");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetShippersResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Shippers);
    }

    [Fact]
    public async Task GetShippers_OneSeeded_ReturnsIt()
    {
        var shipper = await Factory.SeedShipperAsync("Acme Shipping", "contact@acme.com");

        var response = await Client.GetAsync("/shippers");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetShippersResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Contains(body.Shippers, s => s.ShipperId == shipper.Id && s.Name == "Acme Shipping" && s.ContactEmail == "contact@acme.com");
    }

    [Fact]
    public async Task GetShipmentsByShipper_NoShipments_ReturnsEmptyList()
    {
        var shipper = await Factory.SeedShipperAsync();

        var response = await Client.GetAsync($"/shippers/{shipper.Id}/shipments");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetShipmentsByShipperResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Shipments);
    }

    [Fact]
    public async Task GetShipmentsByShipper_UnknownShipper_ReturnsEmptyListRatherThanThrowing()
    {
        // GetShipmentsByShipperHandler never validates the shipper id exists - it just
        // queries shipments by shipper id, which legitimately returns none.
        var response = await Client.GetAsync($"/shippers/{Guid.NewGuid()}/shipments");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetShipmentsByShipperResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Shipments);
    }

    // --- Address book ------------------------------------------------------------------

    [Fact]
    public async Task AddAddress_ThenGetAddresses_ReturnsTheSavedAddressesByName()
    {
        var shipper = await Factory.SeedShipperAsync();

        var first = await Client.PostAsJsonAsync($"/shippers/{shipper.Id}/addresses",
            new { Label = "Main warehouse", AreaName = "1100 Favoriten, Vienna", Latitude = 48.155, Longitude = 16.382 }, JsonOptions);
        first.EnsureSuccessStatusCode();
        var saved = await first.Content.ReadFromJsonAsync<ShipperAddressDto>(JsonOptions);
        Assert.Equal("Main warehouse — 1100 Favoriten, Vienna", saved!.PlaceName);
        var second = await Client.PostAsJsonAsync($"/shippers/{shipper.Id}/addresses",
            new { Label = "Depot", AreaName = "80803 Schwabing, Munich", Latitude = 48.1626, Longitude = 11.5868 }, JsonOptions);
        second.EnsureSuccessStatusCode();

        var body = await Client.GetFromJsonAsync<GetShipperAddressesResponse>($"/shippers/{shipper.Id}/addresses", JsonOptions);

        Assert.Equal(["Depot", "Main warehouse"], body!.Addresses.Select(a => a.Label));
        Assert.Equal(saved.AddressId, body.Addresses[1].AddressId);
    }

    [Fact]
    public async Task AddAddress_BlankLabel_Returns400()
    {
        var shipper = await Factory.SeedShipperAsync();

        var response = await Client.PostAsJsonAsync($"/shippers/{shipper.Id}/addresses",
            new { Label = " ", AreaName = "1100 Favoriten, Vienna", Latitude = 48.155, Longitude = 16.382 }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAddresses_UnknownShipper_Returns400()
    {
        var response = await Client.GetAsync($"/shippers/{Guid.NewGuid()}/addresses");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetShipmentsByShipper_BookedShipment_CarriesItsPlaceNamesAndPickupPosition()
    {
        var shipper = await Factory.SeedShipperAsync();
        var pickupStart = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;
        var book = await Client.PostAsJsonAsync("/shipments", new
        {
            ShipperId = shipper.Id,
            PickupLatitude = 48.155,
            PickupLongitude = 16.382,
            PickupPlaceName = "Main warehouse — 1100 Favoriten, Vienna",
            DeliveryLatitude = 48.1626,
            DeliveryLongitude = 11.5868,
            DeliveryPlaceName = "Depot — 80803 Schwabing, Munich",
            LoadWeightKg = 100.0,
            LoadVolumeCubicMeters = 1.0,
            RequiredTruckType = "Refrigerated",
            PickupWindowEarliest = pickupStart,
            PickupWindowLatest = pickupStart.AddDays(2),
            DeliveryWindowEarliest = pickupStart,
            DeliveryWindowLatest = pickupStart.AddDays(3)
        }, JsonOptions);
        book.EnsureSuccessStatusCode();

        var body = await Client.GetFromJsonAsync<GetShipmentsByShipperResponse>($"/shippers/{shipper.Id}/shipments", JsonOptions);

        var shipment = Assert.Single(body!.Shipments);
        Assert.Equal("Main warehouse — 1100 Favoriten, Vienna", shipment.PickupPlaceName);
        Assert.Equal("Depot — 80803 Schwabing, Munich", shipment.DeliveryPlaceName);
        Assert.Equal(48.155, shipment.CurrentLatitude);
        Assert.Equal(16.382, shipment.CurrentLongitude);
    }

    [Fact]
    public async Task BookShipment_WithoutPlaceNames_Returns400()
    {
        var shipper = await Factory.SeedShipperAsync();
        var pickupStart = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

        var response = await Client.PostAsJsonAsync("/shipments", new
        {
            ShipperId = shipper.Id,
            PickupLatitude = 48.155,
            PickupLongitude = 16.382,
            PickupPlaceName = " ",
            DeliveryLatitude = 48.1626,
            DeliveryLongitude = 11.5868,
            DeliveryPlaceName = "Depot — 80803 Schwabing, Munich",
            LoadWeightKg = 100.0,
            LoadVolumeCubicMeters = 1.0,
            RequiredTruckType = "Refrigerated",
            PickupWindowEarliest = pickupStart,
            PickupWindowLatest = pickupStart.AddDays(2),
            DeliveryWindowEarliest = pickupStart,
            DeliveryWindowLatest = pickupStart.AddDays(3)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
