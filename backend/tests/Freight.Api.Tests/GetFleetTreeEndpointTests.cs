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

public sealed class GetFleetTreeEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public GetFleetTreeEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

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

    private sealed record FleetDriverDto(Guid DriverId, string FirstName, string LastName);

    private sealed record FleetDriverAssignmentDto(
        DriverConfigurationType ConfigurationType,
        FleetDriverDto PrimaryDriver,
        FleetDriverDto? SecondaryDriver,
        Guid? ActiveDriverId);

    private sealed record FleetTruckDto(
        Guid TruckId,
        string TruckName,
        TruckType TruckType,
        TruckSize TruckSize,
        bool IsActive,
        TruckStatus Status,
        FleetDriverAssignmentDto? DriverAssignment);

    private sealed record GetFleetTreeResponse(
        IReadOnlyList<FleetTruckDto> Trucks,
        IReadOnlyList<FleetDriverDto> UnassignedDrivers);

    private static DrivingRules SampleRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    [Fact]
    public async Task GetFleet_ReturnsCompanyTrucksAndGloballyUnassignedDrivers()
    {
        var companyId = Guid.NewGuid();
        var assignedDriver = Driver.Create("Assigned", "Driver", SampleRules());
        var unassignedDriver = Driver.Create("Unassigned", "Driver", SampleRules());

        var truck = Truck.Create("Fleet Truck", TruckType.BoxVan, TruckSize.Medium);
        truck.AssignToCompany(companyId);
        truck.AssignDrivers(assignedDriver);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Driver>().AddRange(assignedDriver, unassignedDriver);
            dbContext.Set<Truck>().Add(truck);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/companies/{companyId}/fleet");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetFleetTreeResponse>(JsonOptions);
        Assert.NotNull(body);

        var truckDto = Assert.Single(body!.Trucks, t => t.TruckId == truck.Id);
        Assert.Equal(assignedDriver.Id, truckDto.DriverAssignment!.PrimaryDriver.DriverId);
        Assert.Contains(body.UnassignedDrivers, d => d.DriverId == unassignedDriver.Id);
        Assert.DoesNotContain(body.UnassignedDrivers, d => d.DriverId == assignedDriver.Id);
    }
}
