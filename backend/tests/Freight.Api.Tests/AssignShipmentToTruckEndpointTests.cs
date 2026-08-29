using System.Net;
using System.Net.Http.Json;
using Freight.Domain.Fleet;
using Freight.Domain.Client;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShipmentAggregate = Freight.Domain.Client.Shipment;

namespace Freight.Api.Tests;

public sealed class AssignShipmentToTruckEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public AssignShipmentToTruckEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FreightDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record AssignShipmentToTruckBody(
        Guid ShipmentId, int PickupInsertIndex = 0, int DeliveryInsertIndex = 0, DateTime? TripStartTime = null);

    private sealed record AssignShipmentToTruckResponse(int StopCount);

    private static DrivingRules SampleRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static ShipmentAggregate NewShipment(TruckType requiredType, Capacity? load = null) =>
        ShipmentAggregate.Book(
            Guid.NewGuid(),
            GeoLocation.Create(52.5, 13.4),
            GeoLocation.Create(48.1, 11.6),
            load ?? Capacity.Create(100, 2),
            requiredType,
            TimeWindow.Create(DateTime.UtcNow, DateTime.UtcNow.AddHours(2)),
            TimeWindow.Create(DateTime.UtcNow.AddHours(4), DateTime.UtcNow.AddHours(6)),
            DateTime.UtcNow);

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
            TimeWindow.Create(DateTime.UtcNow, DateTime.UtcNow.AddDays(7)),
            TimeWindow.Create(DateTime.UtcNow, DateTime.UtcNow.AddDays(7)),
            DateTime.UtcNow);

    private async Task<(Truck Truck, TruckingCompany Company)> SeedAssignableTruckAsync(TruckType type = TruckType.BoxVan)
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", GeoLocation.Create(52.52, 13.405));
        var truck = Truck.Create("Truck 1", type, TruckSize.Medium);
        truck.AssignToCompany(company.Id);
        truck.Activate();
        var driver = Driver.Create("Jane", "Doe", SampleRules());
        truck.AssignDrivers(driver);

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
        Assert.Contains("capacity", body!["error"], StringComparison.OrdinalIgnoreCase);
    }
}
