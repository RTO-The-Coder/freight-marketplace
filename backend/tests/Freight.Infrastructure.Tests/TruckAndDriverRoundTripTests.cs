using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Freight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Tests;

public class TruckAndDriverRoundTripTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=freight_marketplace;Username=freight;Password=freight_dev_password";

    private static DbContextOptions<FreightDbContext> Options() =>
        new DbContextOptionsBuilder<FreightDbContext>().UseNpgsql(ConnectionString).Options;

    public async Task InitializeAsync()
    {
        await using var dbContext = new FreightDbContext(Options());
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static DrivingRules SampleRules() =>
        DrivingRules.Create(DrivingBreakRule.SplitBreak, DailyRestRule.ReducedRest, WeeklyRestRule.ReducedWeeklyRest, extendDailyDrivingWhenEligible: true);

    [Fact]
    public async Task Driver_PersistedAndReloaded_RoundTripsRules()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SampleRules());

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<Driver>().Add(driver);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await readContext.Set<Driver>().FirstAsync(d => d.Id == driver.Id);

        Assert.Equal(driver.FirstName, reloaded.FirstName);
        Assert.Equal(driver.LastName, reloaded.LastName);
        Assert.Equal(driver.Rules.BreakRule, reloaded.Rules.BreakRule);
        Assert.Equal(driver.Rules.DailyRestRule, reloaded.Rules.DailyRestRule);
        Assert.Equal(driver.Rules.WeeklyRestRule, reloaded.Rules.WeeklyRestRule);
        Assert.Equal(driver.Rules.ExtendDailyDrivingWhenEligible, reloaded.Rules.ExtendDailyDrivingWhenEligible);
    }

    [Fact]
    public async Task Truck_PersistedAndReloaded_RoundTripsSizeDerivedCapacity()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<Truck>().Add(truck);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await readContext.Set<Truck>().FirstAsync(t => t.Id == truck.Id);

        Assert.Equal(truck.TruckName, reloaded.TruckName);
        Assert.Equal(truck.TruckType, reloaded.TruckType);
        Assert.Equal(truck.TruckSize, reloaded.TruckSize);
        Assert.Equal(truck.Capacity.Total.WeightKg, reloaded.Capacity.Total.WeightKg);
        Assert.Equal(truck.Capacity.Total.VolumeCubicMeters, reloaded.Capacity.Total.VolumeCubicMeters);
        Assert.False(reloaded.IsActive);
        Assert.Null(reloaded.TruckingCompanyId);
    }

    [Fact]
    public async Task Truck_WithSingleDriver_RoundTripsDriverAssignmentByReference()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Primary", "Driver", SampleRules());
        var truck = Truck.Create(Guid.NewGuid(), "Truck-2", TruckType.Flatbed, TruckSize.Large);
        truck.AssignDrivers(driver);
        truck.SetActiveDriver(driver.Id);

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<Driver>().Add(driver);
            writeContext.Set<Truck>().Add(truck);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await readContext.Set<Truck>().FirstAsync(t => t.Id == truck.Id);

        Assert.NotNull(reloaded.DriverAssignment);
        Assert.Equal(DriverConfigurationType.Single, reloaded.DriverAssignment!.ConfigurationType);
        Assert.Equal(driver.Id, reloaded.DriverAssignment.PrimaryDriver.Id);
        Assert.Null(reloaded.DriverAssignment.SecondaryDriver);
        Assert.Equal(driver.Id, reloaded.DriverAssignment.ActiveDriverId);
    }

    [Fact]
    public async Task Truck_WithTeamDrivers_RoundTripsBothDriverReferences()
    {
        var primary = Driver.Create(Guid.NewGuid(), "Primary", "Driver", SampleRules());
        var secondary = Driver.Create(Guid.NewGuid(), "Secondary", "Driver", SampleRules());
        var truck = Truck.Create(Guid.NewGuid(), "Truck-3", TruckType.Tanker, TruckSize.Large);
        truck.AssignDrivers(primary, secondary);

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<Driver>().AddRange(primary, secondary);
            writeContext.Set<Truck>().Add(truck);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await readContext.Set<Truck>().FirstAsync(t => t.Id == truck.Id);

        Assert.Equal(DriverConfigurationType.Team, reloaded.DriverAssignment!.ConfigurationType);
        Assert.Equal(primary.Id, reloaded.DriverAssignment.PrimaryDriver.Id);
        Assert.Equal(secondary.Id, reloaded.DriverAssignment.SecondaryDriver!.Id);
    }

    [Fact]
    public async Task Trip_WithAssignedShipmentStops_RoundTripsRouteStopsInOrder()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Route", "Driver", SampleRules());
        var truck = Truck.Create(Guid.NewGuid(), "Truck-4", TruckType.BoxVan, TruckSize.Small);
        var company = TruckingCompany.Create(Guid.NewGuid(), "Route Co", GeoLocation.Create(52.52, 13.405));
        truck.AssignToCompany(company.Id);
        truck.AssignDrivers(driver);

        var trip = Trip.Open(truck.Id, company.Id, new DateTime(2026, 1, 1, 6, 0, 0, DateTimeKind.Utc));

        var shipmentId = Guid.NewGuid();
        truck.AssignShipment(
            trip,
            shipmentId,
            Capacity.Create(100, 2),
            GeoLocation.Create(52.5, 13.4),
            GeoLocation.Create(48.1, 11.6),
            company.OfficeLocation,
            pickupInsertIndex: 0,
            deliveryInsertIndex: 0,
            pickupLegDistanceKm: 650, pickupLegTimeTick: 78,
            deliveryLegDistanceKm: 650, deliveryLegTimeTick: 78,
            officeLegDistanceKm: 650, officeLegTimeTick: 78);

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<TruckingCompany>().Add(company);
            writeContext.Set<Driver>().Add(driver);
            writeContext.Set<Truck>().Add(truck);
            writeContext.Set<Trip>().Add(trip);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await readContext.Set<Trip>().FirstAsync(t => t.Id == trip.Id);

        Assert.Equal(3, reloaded.Stops.Count);
        Assert.All(reloaded.Stops.Where(s => s.Kind != StopKind.Office), s => Assert.Equal(shipmentId, s.ShipmentId));
        Assert.Contains(reloaded.Stops, s => s.Kind == StopKind.Pickup);
        Assert.Contains(reloaded.Stops, s => s.Kind == StopKind.Delivery);
        Assert.Contains(reloaded.Stops, s => s.Kind == StopKind.Office);
        Assert.Equal(StopKind.Office, reloaded.Stops[^1].Kind);
        Assert.All(reloaded.Stops, s => Assert.Equal(StopStatus.Pending, s.Status));
    }
}
