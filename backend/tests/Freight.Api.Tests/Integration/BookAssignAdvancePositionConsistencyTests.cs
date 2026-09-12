using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Fleet;
using Freight.Application.Simulation;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Api.Tests.Integration;

/// <summary>
/// Chains 6 controllers over real HTTP (drivers, trucks, shippers, shipments, simulation)
/// against the real Postgres-backed handlers - book a shipment, assign it to a truck,
/// advance the simulation partway through the leg, then confirm GetTruckPositionHandler
/// and GetTruckEtasHandler (reached through two separate HTTP round-trips, each its own
/// request/response/DB-read cycle) agree on the same stop and progress fraction. No
/// single-controller test can catch a divergence here, since each mocks nothing but each
/// endpoint is still its own independent request against the live pipeline.
/// </summary>
public sealed class BookAssignAdvancePositionConsistencyTests : ApiTestBase
{
    private static readonly DateTime ClockStart = new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

    [Fact]
    public async Task PositionAndEtaProjection_AgreeOnSameLegAndProgressFraction_AfterPartialAdvanceOverHttp()
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
        var shipmentId = await BookShipmentAsync(shipper.Id);

        var assignResponse = await Client.PostAsJsonAsync($"/trucks/{truckId}/assign-shipment", new
        {
            ShipmentId = shipmentId,
            PickupInsertIndex = 0,
            DeliveryInsertIndex = 0
        }, JsonOptions);
        assignResponse.EnsureSuccessStatusCode();

        // 6-tick leg; advance 3 (half way).
        var advanceResponse = await Client.PostAsJsonAsync("/simulation/advance", new { Ticks = 3 }, JsonOptions);
        advanceResponse.EnsureSuccessStatusCode();

        var position = await Client.GetFromJsonAsync<TruckPositionDto>($"/trucks/{truckId}/position", JsonOptions);
        var etas = await Client.GetFromJsonAsync<TruckEtasDto>($"/trucks/{truckId}/etas", JsonOptions);

        Assert.NotNull(position);
        Assert.NotNull(etas);

        var pendingPickupStop = etas.Stops.First(s => s.Kind == StopKind.Pickup);
        Assert.Equal(pendingPickupStop.StopId, position.HeadingToStopId);
        Assert.Equal(0.5, position.LegProgressFraction);
    }

    private async Task<Guid> BookShipmentAsync(Guid shipperId)
    {
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
