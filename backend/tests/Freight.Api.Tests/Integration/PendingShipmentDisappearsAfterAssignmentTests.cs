using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Client;
using Freight.Application.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Api.Tests.Integration;

/// <summary>
/// Chains ShipmentsController and TruckController over real HTTP: book two shipments,
/// confirm both list as Pending, assign one to a truck, and confirm it vanishes from the
/// pending list while the other remains - proving GetPendingShipmentsHandler's status
/// filter and AssignShipmentToTruckHandler's status-mutating side effect stay in lockstep
/// through two genuinely separate HTTP requests against the real DB, not mocked state.
/// </summary>
public sealed class PendingShipmentDisappearsAfterAssignmentTests : ApiTestBase
{
    private static readonly DateTime ClockStart = new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

    [Fact]
    public async Task AssignedShipment_VanishesFromPendingListOverHttp_ButOtherShipmentRemains()
    {
        Factory.RoutingService.DefaultLeg = new RouteLeg(DistanceKm: 20, TimeTicks: 6);
        await Client.PostAsJsonAsync("/simulation/time", new { NewCurrentTime = ClockStart }, JsonOptions);

        var company = await Factory.SeedTruckingCompanyAsync();
        var truckId = await AddTruckAsync();
        await Client.PostAsJsonAsync($"/trucks/{truckId}/company", new { TruckingCompanyId = company.Id }, JsonOptions);
        var driverId = await AddDriverAsync();
        await Client.PatchAsJsonAsync($"/trucks/{truckId}/drivers", new { PrimaryDriverId = driverId, SecondaryDriverId = (Guid?)null }, JsonOptions);
        await Client.PostAsync($"/trucks/{truckId}/activate", null);

        var shipper = await Factory.SeedShipperAsync();
        var shipmentAId = await BookShipmentAsync(shipper.Id, 52.52, 13.405);
        var shipmentBId = await BookShipmentAsync(shipper.Id, 52.0, 13.0);

        var pendingBefore = await Client.GetFromJsonAsync<GetPendingShipmentsResponse>("/shipments/pending", JsonOptions);
        Assert.Equal(2, pendingBefore!.Shipments.Count);

        var assignResponse = await Client.PostAsJsonAsync($"/trucks/{truckId}/assign-shipment", new
        {
            ShipmentId = shipmentAId,
            PickupInsertIndex = 0,
            DeliveryInsertIndex = 0
        }, JsonOptions);
        assignResponse.EnsureSuccessStatusCode();

        var pendingAfter = await Client.GetFromJsonAsync<GetPendingShipmentsResponse>("/shipments/pending", JsonOptions);
        Assert.Single(pendingAfter!.Shipments);
        Assert.Equal(shipmentBId, pendingAfter.Shipments[0].ShipmentId);

        var shipperHistory = await Client.GetFromJsonAsync<GetShipmentsByShipperResponse>($"/shippers/{shipper.Id}/shipments", JsonOptions);
        Assert.Equal(2, shipperHistory!.Shipments.Count);
        var shipmentADto = shipperHistory.Shipments.First(s => s.ShipmentId == shipmentAId);
        Assert.Equal(Domain.Client.Enums.ShipmentStatus.Booked, shipmentADto.Status);
        Assert.Equal(company.Id, shipmentADto.TruckingCompanyId);
    }

    private async Task<Guid> BookShipmentAsync(Guid shipperId, double pickupLat, double pickupLng)
    {
        var response = await Client.PostAsJsonAsync("/shipments", new
        {
            ShipperId = shipperId,
            PickupLatitude = pickupLat,
            PickupLongitude = pickupLng,
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
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BookShipmentIdOnly>(JsonOptions);
        return body!.ShipmentId;
    }

    private sealed record BookShipmentIdOnly(Guid ShipmentId);

    private async Task<Guid> AddTruckAsync()
    {
        var response = await Client.PostAsJsonAsync("/trucks", new
        {
            TruckName = "Truck-1",
            TruckType = TruckType.Refrigerated,
            TruckSize = TruckSize.Medium
        }, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<AddTruckResponse>(JsonOptions);
        return body!.TruckId;
    }

    private async Task<Guid> AddDriverAsync()
    {
        var response = await Client.PostAsJsonAsync("/drivers", new
        {
            FirstName = "Jane",
            LastName = "Doe",
            BreakRule = DrivingBreakRule.FullBreak,
            DailyRestRule = DailyRestRule.FullRest,
            WeeklyRestRule = WeeklyRestRule.FullWeeklyRest,
            ExtendDailyDrivingWhenEligible = false
        }, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<AddDriverResponse>(JsonOptions);
        return body!.DriverId;
    }
}
