using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Client;
using Freight.Domain.Fleet.Enums;

namespace Freight.Api.Tests.Controllers;

public sealed class ShipmentsControllerTests : ApiTestBase
{
    [Fact]
    public async Task GetPendingShipments_NoneExist_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/shipments/pending");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetPendingShipmentsResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Shipments);
    }

    [Fact]
    public async Task BookShipment_ValidBody_Returns200AndAppearsInPending()
    {
        var shipper = await Factory.SeedShipperAsync();

        var bookResponse = await BookShipmentAsync(shipper.Id);

        var pending = await Client.GetFromJsonAsync<GetPendingShipmentsResponse>("/shipments/pending", JsonOptions);
        Assert.NotNull(pending);
        Assert.Contains(pending.Shipments, s => s.ShipmentId == bookResponse.ShipmentId);
    }

    [Fact]
    public async Task BookShipment_DeliveryWindowEntirelyBeforePickupWindow_Returns200RatherThanThrowing()
    {
        // Shipment.Book validates each TimeWindow internally (earliest < latest) but never
        // cross-checks pickup vs. delivery window ordering against each other - this pins
        // that (possibly surprising) real behavior rather than assuming validation exists.
        var shipper = await Factory.SeedShipperAsync();
        var pickupStart = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

        var response = await Client.PostAsJsonAsync("/shipments", new
        {
            ShipperId = shipper.Id,
            PickupLatitude = 52.52,
            PickupLongitude = 13.405,
            DeliveryLatitude = 48.1351,
            DeliveryLongitude = 11.582,
            LoadWeightKg = 100.0,
            LoadVolumeCubicMeters = 1.0,
            RequiredTruckType = TruckType.Refrigerated,
            PickupWindowEarliest = pickupStart,
            PickupWindowLatest = pickupStart.AddDays(2),
            DeliveryWindowEarliest = pickupStart.AddDays(-2),
            DeliveryWindowLatest = pickupStart.AddDays(-1)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BookShipment_PickupWindowEarliestEqualsLatest_Returns400ViaExceptionMiddleware()
    {
        // TimeWindow.Create DOES reject earliest >= latest - this is the genuine
        // validation boundary, unlike the cross-window case above.
        var shipper = await Factory.SeedShipperAsync();
        var sameInstant = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

        var response = await Client.PostAsJsonAsync("/shipments", new
        {
            ShipperId = shipper.Id,
            PickupLatitude = 52.52,
            PickupLongitude = 13.405,
            DeliveryLatitude = 48.1351,
            DeliveryLongitude = 11.582,
            LoadWeightKg = 100.0,
            LoadVolumeCubicMeters = 1.0,
            RequiredTruckType = TruckType.Refrigerated,
            PickupWindowEarliest = sameInstant,
            PickupWindowLatest = sameInstant,
            DeliveryWindowEarliest = sameInstant,
            DeliveryWindowLatest = sameInstant.AddDays(1)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePickupWindow_UnknownShipment_Returns400ViaExceptionMiddleware()
    {
        var newEarliest = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

        var response = await Client.PatchAsJsonAsync($"/shipments/{Guid.NewGuid()}/pickup-window", new
        {
            PickupWindowEarliest = newEarliest,
            PickupWindowLatest = newEarliest.AddDays(1)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePickupWindow_ValidBody_Returns204AndUpdatesWindow()
    {
        var shipper = await Factory.SeedShipperAsync();
        var bookResponse = await BookShipmentAsync(shipper.Id);
        var newEarliest = new DateTimeOffset(2026, 3, 5, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

        var response = await Client.PatchAsJsonAsync($"/shipments/{bookResponse.ShipmentId}/pickup-window", new
        {
            PickupWindowEarliest = newEarliest,
            PickupWindowLatest = newEarliest.AddDays(1)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var pending = await Client.GetFromJsonAsync<GetPendingShipmentsResponse>("/shipments/pending", JsonOptions);
        var shipment = pending!.Shipments.Single(s => s.ShipmentId == bookResponse.ShipmentId);
        Assert.Equal(newEarliest, shipment.PickupWindowEarliest);
    }

    private async Task<BookShipmentResponse> BookShipmentAsync(Guid shipperId)
    {
        var pickupStart = new DateTimeOffset(2026, 3, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

        var response = await Client.PostAsJsonAsync("/shipments", new
        {
            ShipperId = shipperId,
            PickupLatitude = 52.52,
            PickupLongitude = 13.405,
            DeliveryLatitude = 48.1351,
            DeliveryLongitude = 11.582,
            LoadWeightKg = 100.0,
            LoadVolumeCubicMeters = 1.0,
            RequiredTruckType = TruckType.Refrigerated,
            PickupWindowEarliest = pickupStart,
            PickupWindowLatest = pickupStart.AddDays(2),
            DeliveryWindowEarliest = pickupStart,
            DeliveryWindowLatest = pickupStart.AddDays(3)
        }, JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BookShipmentResponse>(JsonOptions))!;
    }
}
