using System.Net;
using System.Net.Http.Json;
using Freight.Domain.Fleet;
using Freight.Domain.Client;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipmentAggregate = Freight.Domain.Client.Shipment;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Client.Enums;

namespace Freight.Api.Tests;

public sealed class AssignShipmentToTruckEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public AssignShipmentToTruckEndpointTests(WebApplicationFactory<Program> factory) =>
        // Swap the OSRM-backed routing service for a stub so the assign-shipment flow
        // never makes a live HTTP call during CI. Every leg comes back as a fixed
        // 650 km / 78 tick segment.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRoutingService>();
                services.AddSingleton<IRoutingService, StubRoutingService>();
            }));

    // A fixed instant the sim clock is pinned to in InitializeAsync, so feasibility
    // projections (which start from the sim clock / trip StartedAt) are deterministic
    // regardless of what an earlier test or the seeder left the shared clock at. All
    // test shipment windows are anchored to this instant, not DateTime.UtcNow.
    private static readonly DateTime SimNow = new(2026, 6, 1, 6, 0, 0, DateTimeKind.Utc);

    private sealed record SetSimulationTimeBody(DateTime NewCurrentTime);

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();

        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/simulation/time", new SetSimulationTimeBody(SimNow));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record AssignShipmentToTruckBody(
        Guid ShipmentId, int PickupInsertIndex = 0, int DeliveryInsertIndex = 0, DateTime? TripStartTime = null);

    private sealed record AssignShipmentToTruckResponse(int StopCount);

    private sealed record ShipmentFeasibilityResponse(bool IsFeasible, Guid? ViolatingStopId, string? Reason);

    private static DrivingRules SampleRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static ShipmentAggregate NewShipment(TruckType requiredType, Capacity? load = null) =>
        ShipmentAggregate.Book(
            Guid.NewGuid(),
            GeoLocation.Create(52.5, 13.4),
            GeoLocation.Create(48.1, 11.6),
            load ?? Capacity.Create(100, 2),
            requiredType,
            TimeWindow.Create(SimNow, SimNow.AddHours(2)),
            TimeWindow.Create(SimNow.AddHours(4), SimNow.AddHours(6)),
            SimNow);

    /// <summary>
    /// Like <see cref="NewShipment"/> but with pickup/delivery windows wide enough that
    /// the Q2 route/window feasibility check (projected arrival across a ~6.5h placeholder
    /// leg) always passes - for the happy-path assignment test.
    /// </summary>
    private static ShipmentAggregate NewShipmentWithWideWindows(TruckType requiredType) =>
        ShipmentAggregate.Book(
            Guid.NewGuid(),
            GeoLocation.Create(52.5, 13.4),
            GeoLocation.Create(48.1, 11.6),
            Capacity.Create(100, 2),
            requiredType,
            TimeWindow.Create(SimNow, SimNow.AddDays(7)),
            TimeWindow.Create(SimNow, SimNow.AddDays(7)),
            SimNow);

    private async Task<(Truck Truck, TruckingCompany Company)> SeedAssignableTruckAsync(TruckType type = TruckType.BoxVan)
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", GeoLocation.Create(52.52, 13.405));
        var truck = Truck.Create("Truck 1", type, TruckSize.Medium);
        truck.AssignToCompany(company.Id);
        var driver = Driver.Create("Jane", "Doe", SampleRules());
        truck.AssignDrivers(driver);
        truck.Activate();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        dbContext.Set<TruckingCompany>().Add(company);
        dbContext.Set<Driver>().Add(driver);
        dbContext.Set<Truck>().Add(truck);
        await dbContext.SaveChangesAsync();

        return (truck, company);
    }

    [Fact]
    public async Task AssignShipment_ValidRequest_Returns200AndOpensTripWithThreeStops()
    {
        var (truck, _) = await SeedAssignableTruckAsync();
        // Wide pickup/delivery windows so the Q2 route/window feasibility check passes -
        // the placeholder leg is 6.5h, and the projected arrival must land inside the window.
        var shipment = NewShipmentWithWideWindows(TruckType.BoxVan);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<ShipmentAggregate>().Add(shipment);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/trucks/{truck.Id}/assign-shipment",
            new AssignShipmentToTruckBody(shipment.Id, PickupInsertIndex: 0, DeliveryInsertIndex: 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AssignShipmentToTruckResponse>();
        Assert.NotNull(body);
        Assert.Equal(3, body!.StopCount);

        using var readScope = _factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<FreightDbContext>();

        var persistedTrip = await readContext.Set<Trip>()
            .Include(trip => trip.Stops)
            .FirstAsync(trip => trip.TruckId == truck.Id);
        Assert.Equal(3, persistedTrip.Stops.Count);
        Assert.Equal(StopKind.Office, persistedTrip.Stops.OrderBy(s => s.Sequence).Last().Kind);

        var persistedShipment = await readContext.Set<ShipmentAggregate>().FirstAsync(s => s.Id == shipment.Id);
        Assert.Equal(ShipmentStatus.Booked, persistedShipment.Status);
    }

    [Fact]
    public async Task CheckFeasibility_ViableInsertion_Returns200IsFeasibleTrueAndPersistsNoTrip()
    {
        var (truck, _) = await SeedAssignableTruckAsync();
        var shipment = NewShipmentWithWideWindows(TruckType.BoxVan);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<ShipmentAggregate>().Add(shipment);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/trucks/{truck.Id}/assign-shipment/feasibility",
            new AssignShipmentToTruckBody(shipment.Id, PickupInsertIndex: 0, DeliveryInsertIndex: 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShipmentFeasibilityResponse>();
        Assert.NotNull(body);
        Assert.True(body!.IsFeasible);

        using var readScope = _factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<FreightDbContext>();
        Assert.False(await readContext.Set<Trip>().AnyAsync(t => t.TruckId == truck.Id));
    }

    [Fact]
    public async Task CheckFeasibility_PickupWindowUnreachable_Returns200IsFeasibleFalseWithReason()
    {
        var (truck, _) = await SeedAssignableTruckAsync();
        var shipment = NewShipment(TruckType.BoxVan);   // 2h pickup window - unreachable across the stub's 6.5h leg

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<ShipmentAggregate>().Add(shipment);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/trucks/{truck.Id}/assign-shipment/feasibility",
            new AssignShipmentToTruckBody(shipment.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShipmentFeasibilityResponse>();
        Assert.NotNull(body);
        Assert.False(body!.IsFeasible);
        Assert.False(string.IsNullOrWhiteSpace(body.Reason));
    }

    [Fact]
    public async Task AssignShipment_TruckTypeMismatch_Returns400WithMessage()
    {
        var (truck, _) = await SeedAssignableTruckAsync(TruckType.Flatbed);
        var shipment = NewShipment(TruckType.Refrigerated);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<ShipmentAggregate>().Add(shipment);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/trucks/{truck.Id}/assign-shipment",
            new AssignShipmentToTruckBody(shipment.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.Contains("type", body!["error"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignShipment_ExceedsCapacity_Returns400WithMessage()
    {
        var (truck, _) = await SeedAssignableTruckAsync();
        var shipment = NewShipment(TruckType.BoxVan, Capacity.Create(truck.Capacity.WeightKg + 1, 5));

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
            dbContext.Set<ShipmentAggregate>().Add(shipment);
            await dbContext.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/trucks/{truck.Id}/assign-shipment",
            new AssignShipmentToTruckBody(shipment.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(body);
        Assert.Contains("overloaded", body!["error"], StringComparison.OrdinalIgnoreCase);
    }
}
