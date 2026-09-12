using System.Net;
using System.Net.Http.Json;
using Freight.Api.Tests.TestSupport;
using Freight.Application.Fleet;
using Freight.Application.Tracking;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Api.Tests.Controllers;

/// <summary>
/// Exercises DriversController's HTTP surface: routing, status codes (including
/// CreatedAtAction's Location header), model binding, and the exception middleware -
/// not handler business logic, which Freight.Application.Tests already covers per
/// handler.
/// </summary>
public sealed class DriversControllerTests : ApiTestBase
{
    [Fact]
    public async Task AddDriver_ValidBody_Returns201WithLocationHeaderAndFlattenedRules()
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

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AddDriverResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains(body.DriverId.ToString(), response.Headers.Location!.ToString());
        Assert.Equal(DrivingBreakRule.FullBreak, body.BreakRule);
        Assert.Equal(DailyRestRule.FullRest, body.DailyRestRule);
        Assert.Equal(WeeklyRestRule.FullWeeklyRest, body.WeeklyRestRule);
        Assert.False(body.ExtendDailyDrivingWhenEligible);
    }

    [Fact]
    public async Task GetDrivers_NoneExist_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/drivers?unassigned=false");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Drivers);
    }

    [Fact]
    public async Task GetDrivers_UnassignedFilter_ExcludesAssignedDriver()
    {
        var driverId = await AddDriverAsync();
        var truckId = await AddTruckAsync();
        await Client.PatchAsJsonAsync($"/trucks/{truckId}/drivers", new { PrimaryDriverId = driverId, SecondaryDriverId = (Guid?)null }, JsonOptions);

        var response = await Client.GetAsync("/drivers?unassigned=true");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body.Drivers);
    }

    [Fact]
    public async Task GetTruckForDriver_Unassigned_ReturnsNullTruck()
    {
        var driverId = await AddDriverAsync();

        var response = await Client.GetAsync($"/drivers/{driverId}/truck");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetTruckForDriverResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Null(body.Truck);
    }

    [Fact]
    public async Task GetTruckForDriver_Assigned_ReturnsTruckSummary()
    {
        var driverId = await AddDriverAsync();
        var truckId = await AddTruckAsync();
        await Client.PatchAsJsonAsync($"/trucks/{truckId}/drivers", new { PrimaryDriverId = driverId, SecondaryDriverId = (Guid?)null }, JsonOptions);

        var response = await Client.GetAsync($"/drivers/{driverId}/truck");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<GetTruckForDriverResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotNull(body.Truck);
        Assert.Equal(truckId, body.Truck.TruckId);
    }

    [Fact]
    public async Task GetDriverDetail_UnknownDriver_Returns400ViaExceptionMiddleware()
    {
        var response = await Client.GetAsync($"/drivers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDriverDetail_NewlyAddedDriver_HasNullComplianceState()
    {
        var driverId = await AddDriverAsync();

        var response = await Client.GetAsync($"/drivers/{driverId}");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<DriverDetailDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(driverId, body.DriverId);
        Assert.Null(body.ComplianceState);
    }

    [Fact]
    public async Task CheckDriverEligibility_NoComplianceLedgerYet_Returns400ViaExceptionMiddleware()
    {
        // Driver has never started driving - CheckDriverEligibilityHandler throws when
        // ComplianceState is null.
        var driverId = await AddDriverAsync();

        var response = await Client.PostAsJsonAsync($"/drivers/{driverId}/eligibility-check", new { AfterMinutes = 0 }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckDriverEligibility_NegativeAfterMinutes_Returns400ViaExceptionMiddleware()
    {
        var driverId = await AddDriverAsync();

        var response = await Client.PostAsJsonAsync($"/drivers/{driverId}/eligibility-check", new { AfterMinutes = -1 }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

    private async Task<Guid> AddTruckAsync()
    {
        var response = await Client.PostAsJsonAsync("/trucks", new
        {
            TruckName = "Truck-1",
            TruckType = Domain.Fleet.Enums.TruckType.Refrigerated,
            TruckSize = Domain.Fleet.Enums.TruckSize.Medium
        }, JsonOptions);
        var body = await response.Content.ReadFromJsonAsync<AddTruckResponse>(JsonOptions);
        return body!.TruckId;
    }
}
