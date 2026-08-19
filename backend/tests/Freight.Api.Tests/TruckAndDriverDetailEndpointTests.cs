using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freight.Api.Tests;

public sealed class TruckAndDriverDetailEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public TruckAndDriverDetailEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static DrivingRules SampleRules() =>
        DrivingRules.Create(DrivingBreakRule.SplitBreak, DailyRestRule.ReducedRest, WeeklyRestRule.ReducedWeeklyRest, true);

    private sealed record TruckDetailDriverDto(Guid DriverId, string FirstName, string LastName);

    private sealed record TruckDetailDto(
        Guid TruckId,
        string TruckName,
        TruckType TruckType,
        TruckSize TruckSize,
        bool IsActive,
        TruckStatus Status,
        Guid? TruckingCompanyId,
        DriverConfigurationType? DriverConfigurationType,
        TruckDetailDriverDto? PrimaryDriver,
        TruckDetailDriverDto? SecondaryDriver);

    private sealed record DriverDetailDto(
        Guid DriverId,
        string FirstName,
        string LastName,
        DrivingBreakRule BreakRule,
        DailyRestRule DailyRestRule,
        WeeklyRestRule WeeklyRestRule,
        bool ExtendDailyDrivingWhenEligible);

    private sealed record TruckSummaryDto(
        Guid TruckId,
        string TruckName,
        TruckType TruckType,
        TruckSize TruckSize,
        bool IsActive,
        TruckStatus Status,
        Guid? TruckingCompanyId,
        bool HasDriverAssignment);

    private sealed record GetTruckForDriverResponse(TruckSummaryDto? Truck);

    [Fact]
    public async Task GetTruckDetail_TruckWithTeamDrivers_ReturnsBothDrivers()
    {
        var primary = Driver.Create("DetailPrimary", "Driver", SampleRules());
        var secondary = Driver.Create("DetailSecondary", "Driver", SampleRules());
        var truck = Truck.Create("Detail Truck", TruckType.Tanker, TruckSize.Large);
        truck.AssignDrivers(primary, secondary);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Driver>().AddRange(primary, secondary);
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/trucks/{truck.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TruckDetailDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(primary.Id, body!.PrimaryDriver!.DriverId);
        Assert.Equal(secondary.Id, body.SecondaryDriver!.DriverId);
    }

    [Fact]
    public async Task GetTruckDetail_UnknownTruckId_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/trucks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetDriverDetail_KnownDriver_ReturnsRules()
    {
        var driver = Driver.Create("RuleDriver", "Doe", SampleRules());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Driver>().Add(driver);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/drivers/{driver.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DriverDetailDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(DrivingBreakRule.SplitBreak, body!.BreakRule);
        Assert.True(body.ExtendDailyDrivingWhenEligible);
    }

    [Fact]
    public async Task GetDriverDetail_UnknownDriverId_Returns400()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/drivers/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTruckForDriver_AssignedDriver_ReturnsTruck()
    {
        var driver = Driver.Create("AssignedForTruck", "Driver", SampleRules());
        var truck = Truck.Create("Truck For Driver", TruckType.BoxVan, TruckSize.Small);
        truck.AssignDrivers(driver);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Driver>().Add(driver);
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/drivers/{driver.Id}/truck");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetTruckForDriverResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotNull(body!.Truck);
        Assert.Equal(truck.Id, body.Truck!.TruckId);
    }

    [Fact]
    public async Task GetTruckForDriver_UnassignedDriver_ReturnsNullTruck()
    {
        var driver = Driver.Create("NeverAssigned", "Driver", SampleRules());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Driver>().Add(driver);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/drivers/{driver.Id}/truck");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetTruckForDriverResponse>(JsonOptions);
        Assert.NotNull(body);
        Assert.Null(body!.Truck);
    }
}
