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

public sealed class GetDriversEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public GetDriversEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

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

    private sealed record DriverSummaryDto(Guid DriverId, string FirstName, string LastName);

    private sealed record GetDriversResponse(IReadOnlyList<DriverSummaryDto> Drivers);

    private static DrivingRules SampleRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    [Fact]
    public async Task GetDrivers_UnassignedTrue_IncludesUnassignedExcludesAssigned()
    {
        var assignedDriver = Driver.Create("Assigned", "Driver", SampleRules());
        var unassignedDriver = Driver.Create("Unassigned", "Driver", SampleRules());
        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        truck.AssignDrivers(assignedDriver);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Driver>().AddRange(assignedDriver, unassignedDriver);
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/drivers?unassigned=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>(JsonOptions);
        Assert.NotNull(body);

        Assert.Contains(body!.Drivers, d => d.DriverId == unassignedDriver.Id);
        Assert.DoesNotContain(body.Drivers, d => d.DriverId == assignedDriver.Id);
    }

    [Fact]
    public async Task GetDrivers_UnassignedFalse_IncludesAssignedDriver()
    {
        var assignedDriver = Driver.Create("Assigned2", "Driver", SampleRules());
        var truck = Truck.Create("Truck 2", TruckType.BoxVan, TruckSize.Small);
        truck.AssignDrivers(assignedDriver);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Driver>().Add(assignedDriver);
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/drivers?unassigned=false");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetDriversResponse>(JsonOptions);
        Assert.NotNull(body);

        Assert.Contains(body!.Drivers, d => d.DriverId == assignedDriver.Id);
    }
}
