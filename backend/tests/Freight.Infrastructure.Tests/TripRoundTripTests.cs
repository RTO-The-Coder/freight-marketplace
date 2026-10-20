using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Freight.Infrastructure.Persistence;
using Freight.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Tests;

public sealed class TripRoundTripTests : IAsyncLifetime
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

    private static readonly DateTime StartedAt = new(2026, 1, 1, 6, 0, 0, DateTimeKind.Utc);

    private static Driver SomeDriver(string firstName, string lastName) =>
        Driver.Create(Guid.NewGuid(), firstName, lastName,
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false),
            Guid.NewGuid());

    [Fact]
    public async Task Trip_WithDriversAndRestTime_RoundTripsBoth()
    {
        // No foreign key from Trips to Trucks/TruckingCompanies, so any ids will do.
        var trip = Trip.Open(Guid.NewGuid(), Guid.NewGuid(), StartedAt);
        trip.RecordDriver(SomeDriver("Lena", "Brandt"));
        trip.RecordDriver(SomeDriver("Jonas", "Weber"));
        trip.AccrueRestTick();
        trip.AccrueRestTick();

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<Trip>().Add(trip);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await readContext.Set<Trip>().FirstAsync(t => t.Id == trip.Id);

        Assert.Equal(2, reloaded.RestTimeSoFar);
        Assert.Equal(
            trip.Drivers.Select(d => (d.DriverId, d.FirstName, d.LastName)).OrderBy(d => d.DriverId),
            reloaded.Drivers.Select(d => (d.DriverId, d.FirstName, d.LastName)).OrderBy(d => d.DriverId));
    }

    [Fact]
    public async Task Trip_DriverRecordedLater_IsAddedOnSave()
    {
        var trip = Trip.Open(Guid.NewGuid(), Guid.NewGuid(), StartedAt);
        trip.RecordDriver(SomeDriver("Lena", "Brandt"));

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<Trip>().Add(trip);
            await writeContext.SaveChangesAsync();
        }

        await using (var updateContext = new FreightDbContext(Options()))
        {
            var loaded = await updateContext.Set<Trip>().FirstAsync(t => t.Id == trip.Id);
            loaded.RecordDriver(SomeDriver("Can", "Demir"));
            await updateContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await readContext.Set<Trip>().FirstAsync(t => t.Id == trip.Id);

        Assert.Equal(["Brandt", "Demir"], reloaded.Drivers.Select(d => d.LastName).Order());
    }

    [Fact]
    public async Task GetOpenTripsByCompanyAsync_ReturnsOnlyThatCompanysOpenTrips()
    {
        var companyId = Guid.NewGuid();
        var mine = Trip.Open(Guid.NewGuid(), companyId, StartedAt);
        var others = Trip.Open(Guid.NewGuid(), Guid.NewGuid(), StartedAt);

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<Trip>().AddRange(mine, others);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var trips = await new TripRepository(readContext).GetOpenTripsByCompanyAsync(companyId);

        Assert.Equal([mine.Id], trips.Select(t => t.Id));
    }
}
