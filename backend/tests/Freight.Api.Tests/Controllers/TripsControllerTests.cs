using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Api.Tests.Controllers;

public sealed class TripsControllerTests : ApiTestBase
{
    private static readonly DateTime ClockStart = new DateTimeOffset(2026, 1, 1, 6, 0, 0, TimeSpan.Zero).UtcDateTime;

    [Fact]
    public async Task RescheduleStart_UnknownTrip_Returns400ViaExceptionMiddleware()
    {
        var response = await Client.PatchAsJsonAsync($"/trips/{Guid.NewGuid()}/start", new
        {
            NewStartTime = ClockStart.AddHours(1)
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RescheduleStart_NotYetDepartedTrip_Returns200WithNewStartTime()
    {
        var company = await Factory.SeedTruckingCompanyAsync();
        var truckId = await AddTruckAsync();
        await Client.PostAsJsonAsync($"/trucks/{truckId}/company", new { TruckingCompanyId = company.Id }, JsonOptions);
        var driverId = await AddDriverAsync();
        await Client.PatchAsJsonAsync($"/trucks/{truckId}/drivers", new { PrimaryDriverId = driverId, SecondaryDriverId = (Guid?)null }, JsonOptions);
        await Client.PostAsync($"/trucks/{truckId}/activate", null);

        var shipper = await Factory.SeedShipperAsync();
        var shipmentId = await BookShipmentAsync(shipper.Id);

        var tripStartTime = ClockStart.AddHours(2);
        var assignResponse = await Client.PostAsJsonAsync($"/trucks/{truckId}/assign-shipment", new
        {
            ShipmentId = shipmentId,
            PickupInsertIndex = 0,
            DeliveryInsertIndex = 0,
            TripStartTime = tripStartTime
        }, JsonOptions);
        assignResponse.EnsureSuccessStatusCode();

        var tripId = await GetOpenTripIdAsync(truckId);

        var newStartTime = ClockStart.AddMinutes(10);
        var response = await Client.PatchAsJsonAsync($"/trips/{tripId}/start", new { NewStartTime = newStartTime }, JsonOptions);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RescheduleTripResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(newStartTime, body.StartedAt);
    }

    private async Task<Guid> GetOpenTripIdAsync(Guid truckId)
    {
        var etas = await Client.GetFromJsonAsync<TruckEtasDto>($"/trucks/{truckId}/etas", JsonOptions);
        return etas!.TripId!.Value;
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
