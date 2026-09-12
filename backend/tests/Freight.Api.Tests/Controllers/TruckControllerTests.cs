using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Api.Tests.Controllers;

/// <summary>
/// Exercises TruckController's HTTP surface: routing, status codes (including
/// CreatedAtAction's Location header), model binding, and the exception middleware -
/// not handler business logic, which Freight.Application.Tests already covers per
/// handler. Each test seeds only what its own endpoint needs.
/// </summary>
public sealed class TruckControllerTests : ApiTestBase
{
    [Fact]
    public async Task AddTruck_ValidBody_Returns201WithLocationHeaderAndFlattenedCapacity()
    {
        var response = await Client.PostAsJsonAsync("/trucks", new
        {
            TruckName = "Truck-1",
            TruckType = TruckType.Refrigerated,
            TruckSize = TruckSize.Medium
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AddTruckResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains(body.TruckId.ToString(), response.Headers.Location!.ToString());
        Assert.Equal(9_000, body.CapacityWeightKg);
        Assert.Equal(45, body.CapacityVolumeCubicMeters);
    }

    [Fact]
    public async Task GetTrucks_NoneExist_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/trucks?unassigned=false");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetTrucksResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Trucks);
    }

    [Fact]
    public async Task GetTrucks_FilteredByCompany_ReturnsOnlyThatCompanysTrucks()
    {
        var company = await Factory.SeedTruckingCompanyAsync();
        var truckResponse = await AddTruckAsync();
        await Client.PostAsJsonAsync($"/trucks/{truckResponse.TruckId}/company", new { TruckingCompanyId = company.Id }, JsonOptions);

        var response = await Client.GetAsync($"/trucks?unassigned=false&truckingCompanyId={company.Id}");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetTrucksResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Single(body.Trucks);
        Assert.Equal(truckResponse.TruckId, body.Trucks[0].TruckId);
    }

    [Fact]
    public async Task GetTruckDetail_UnknownTruck_Returns400ViaExceptionMiddleware()
    {
        var response = await Client.GetAsync($"/trucks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AssignTruckToCompany_ThenUnassign_Returns204Both()
    {
        var company = await Factory.SeedTruckingCompanyAsync();
        var truckResponse = await AddTruckAsync();

        var assignResponse = await Client.PostAsJsonAsync($"/trucks/{truckResponse.TruckId}/company", new { TruckingCompanyId = company.Id }, JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, assignResponse.StatusCode);

        var unassignResponse = await Client.DeleteAsync($"/trucks/{truckResponse.TruckId}/company");
        Assert.Equal(HttpStatusCode.NoContent, unassignResponse.StatusCode);

        var detail = await Client.GetFromJsonAsync<TruckDetailDto>($"/trucks/{truckResponse.TruckId}", JsonOptions);
        Assert.Null(detail!.TruckingCompanyId);
    }

    [Fact]
    public async Task ActivateTruck_WithoutCompanyOrDriver_Returns400ViaExceptionMiddleware()
    {
        var truckResponse = await AddTruckAsync();

        var response = await Client.PostAsync($"/trucks/{truckResponse.TruckId}/activate", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ActivateThenDeactivateTruck_Returns204Both()
    {
        var company = await Factory.SeedTruckingCompanyAsync();
        var truckResponse = await AddTruckAsync();
        await Client.PostAsJsonAsync($"/trucks/{truckResponse.TruckId}/company", new { TruckingCompanyId = company.Id }, JsonOptions);
        var driverId = await AddDriverAsync();
        await Client.PatchAsJsonAsync($"/trucks/{truckResponse.TruckId}/drivers", new { PrimaryDriverId = driverId, SecondaryDriverId = (Guid?)null }, JsonOptions);

        var activateResponse = await Client.PostAsync($"/trucks/{truckResponse.TruckId}/activate", null);
        Assert.Equal(HttpStatusCode.NoContent, activateResponse.StatusCode);

        var deactivateResponse = await Client.PostAsync($"/trucks/{truckResponse.TruckId}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, deactivateResponse.StatusCode);

        var detail = await Client.GetFromJsonAsync<TruckDetailDto>($"/trucks/{truckResponse.TruckId}", JsonOptions);
        Assert.False(detail!.IsActive);
    }

    [Fact]
    public async Task AssignDrivers_ThenRemoveDrivers_Returns204Both()
    {
        var truckResponse = await AddTruckAsync();
        var driverId = await AddDriverAsync();

        var assignResponse = await Client.PatchAsJsonAsync($"/trucks/{truckResponse.TruckId}/drivers", new { PrimaryDriverId = driverId, SecondaryDriverId = (Guid?)null }, JsonOptions);
        Assert.Equal(HttpStatusCode.NoContent, assignResponse.StatusCode);

        var removeResponse = await Client.DeleteAsync($"/trucks/{truckResponse.TruckId}/drivers");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var detail = await Client.GetFromJsonAsync<TruckDetailDto>($"/trucks/{truckResponse.TruckId}", JsonOptions);
        Assert.Null(detail!.PrimaryDriver);
        Assert.Null(detail.DriverConfigurationType);
    }

    [Fact]
    public async Task GetTruckEtas_TruckWithoutOpenTrip_ReturnsEmptyStopsRatherThanThrowing()
    {
        var truckResponse = await AddTruckAsync();

        var response = await Client.GetAsync($"/trucks/{truckResponse.TruckId}/etas");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TruckEtasDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Null(body.TripId);
        Assert.Empty(body.Stops);
    }

    [Fact]
    public async Task GetTruckPosition_TruckWithoutOpenTrip_Returns400ViaExceptionMiddleware()
    {
        var truckResponse = await AddTruckAsync();

        var response = await Client.GetAsync($"/trucks/{truckResponse.TruckId}/position");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckAssignShipmentFeasibility_UnknownShipment_Returns400ViaExceptionMiddleware()
    {
        var truckResponse = await AddTruckAsync();

        var response = await Client.PostAsJsonAsync($"/trucks/{truckResponse.TruckId}/assign-shipment/feasibility", new
        {
            ShipmentId = Guid.NewGuid(),
            PickupInsertIndex = 0,
            DeliveryInsertIndex = 0
        }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<AddTruckResponse> AddTruckAsync()
    {
        var response = await Client.PostAsJsonAsync("/trucks", new
        {
            TruckName = "Truck-1",
            TruckType = TruckType.Refrigerated,
            TruckSize = TruckSize.Medium
        }, JsonOptions);
        return (await response.Content.ReadFromJsonAsync<AddTruckResponse>(JsonOptions))!;
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
