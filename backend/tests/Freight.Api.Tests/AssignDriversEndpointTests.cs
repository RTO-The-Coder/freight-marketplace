using System.Net;
using System.Net.Http.Json;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Freight.Api.Tests;

public sealed class AssignDriversEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public AssignDriversEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record AssignDriversBody(Guid PrimaryDriverId, Guid? SecondaryDriverId);

    private static DrivingRules SampleRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    [Fact]
    public async Task PatchTruckDrivers_SecondDriverOnMediumTruck_Returns400WithMessage()
    {
        var truck = Truck.Create("Medium Truck", TruckType.BoxVan, TruckSize.Medium);
        var primary = Driver.Create("Primary", "Driver", SampleRules());
        var secondary = Driver.Create("Secondary", "Driver", SampleRules());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().Add(truck);
            dbContext.Set<Driver>().AddRange(primary, secondary);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            $"/trucks/{truck.Id}/drivers",
            new AssignDriversBody(primary.Id, secondary.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.Contains("Large", body!["error"]);
    }

    [Fact]
    public async Task PatchTruckDrivers_SingleDriverOnMediumTruck_Returns204AndPersistsAssignment()
    {
        var truck = Truck.Create("Medium Truck 2", TruckType.BoxVan, TruckSize.Medium);
        var primary = Driver.Create("Solo", "Driver", SampleRules());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().Add(truck);
            dbContext.Set<Driver>().Add(primary);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            $"/trucks/{truck.Id}/drivers",
            new AssignDriversBody(primary.Id, null));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var readContext = scope2.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await readContext.Set<Truck>().FirstAsync(t => t.Id == truck.Id);
        Assert.NotNull(persisted.DriverAssignment);
        Assert.Equal(primary.Id, persisted.DriverAssignment!.PrimaryDriver.Id);
    }

    [Fact]
    public async Task PatchTruckDrivers_TwoDriversOnLargeTruck_Returns204AndPersistsBoth()
    {
        var truck = Truck.Create("Large Truck", TruckType.BoxVan, TruckSize.Large);
        var primary = Driver.Create("PrimaryLarge", "Driver", SampleRules());
        var secondary = Driver.Create("SecondaryLarge", "Driver", SampleRules());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<Truck>().Add(truck);
            dbContext.Set<Driver>().AddRange(primary, secondary);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PatchAsJsonAsync(
            $"/trucks/{truck.Id}/drivers",
            new AssignDriversBody(primary.Id, secondary.Id));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var readContext = scope2.ServiceProvider.GetRequiredService<FreightDbContext>();
        var persisted = await readContext.Set<Truck>().FirstAsync(t => t.Id == truck.Id);
        Assert.Equal(primary.Id, persisted.DriverAssignment!.PrimaryDriver.Id);
        Assert.Equal(secondary.Id, persisted.DriverAssignment.SecondaryDriver!.Id);
    }
}
