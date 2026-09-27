using Freight.Domain.Client;
using Freight.Domain.Fleet;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Freight.Integration.Tests.TestSupport;

/// <summary>
/// Boots the real API in-process against a throwaway Postgres database
/// ("freight_integrationtests_&lt;guid&gt;") that is migrated before a test class runs and
/// dropped after it. Routing is whatever <see cref="IRoutingService"/> the scenario passes
/// in, so leg durations are fixed by the scenario's data rather than by OSRM.
/// </summary>
public sealed class IntegrationTestFactory(IRoutingService routingService) : WebApplicationFactory<Program>
{
    private const string ServerConnectionString =
        "Host=localhost;Port=5432;Username=freight;Password=freight_dev_password";

    public string DatabaseName { get; } = $"freight_integrationtests_{Guid.NewGuid():N}";

    public string ConnectionString => $"{ServerConnectionString};Database={DatabaseName}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting rather than ConfigureAppConfiguration: Program.cs reads the FCM path
        // eagerly at startup, and only host settings are guaranteed to be visible that early.
        builder.UseSetting("ConnectionStrings:FreightDb", ConnectionString);

        // Blank FCM path keeps Program.cs off the FirebaseApp.Create branch: that call throws
        // on the second test class in the same process, and would send real pushes on booking.
        builder.UseSetting("Fcm:ServiceAccountPath", string.Empty);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRoutingService>();
            services.AddSingleton(routingService);
        });
    }

    public async Task CreateDatabaseAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public async Task DropDatabaseAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.EnsureDeletedAsync();
    }

    /// <summary>Companies are seeded, not created through the API - there is no endpoint for them.</summary>
    public async Task<TruckingCompany> SeedTruckingCompanyAsync(GeoLocation officeLocation)
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), "Integration Trucking", officeLocation);

        await using var dbContext = CreateDbContext();
        dbContext.Set<TruckingCompany>().Add(company);
        await dbContext.SaveChangesAsync();

        return company;
    }

    /// <summary>Shippers are likewise seeded - there is no endpoint for them.</summary>
    public async Task<Shipper> SeedShipperAsync()
    {
        var shipper = Shipper.Create(Guid.NewGuid(), "Integration Shipping", "shipping@integration.test");

        await using var dbContext = CreateDbContext();
        dbContext.Set<Shipper>().Add(shipper);
        await dbContext.SaveChangesAsync();

        return shipper;
    }

    /// <summary>
    /// The truck's single trip, open or completed, with its stops. The API only exposes a
    /// truck's open trip, so once a trip completes this is the only way to inspect it.
    /// </summary>
    public async Task<Trip> LoadOnlyTripOfTruckAsync(Guid truckId)
    {
        await using var dbContext = CreateDbContext();
        return await dbContext.Set<Trip>().AsNoTracking().SingleAsync(trip => trip.TruckId == truckId);
    }

    /// <summary>
    /// Which driver is at the wheel of a team truck. The API does not expose it, so it is
    /// read straight from the truck's driver assignment.
    /// </summary>
    public async Task<Guid?> LoadActiveDriverIdAsync(Guid truckId)
    {
        await using var dbContext = CreateDbContext();
        var truck = await dbContext.Set<Truck>().AsNoTracking().SingleAsync(t => t.Id == truckId);
        return truck.DriverAssignment?.ActiveDriverId;
    }

    /// <summary>
    /// Drops every database this project created and kept (failed tests keep theirs for
    /// inspection). Never call from a normal test run: tests run in parallel, so this would
    /// drop the database of a test that is still running.
    /// </summary>
    public static async Task<IReadOnlyList<string>> DropKeptDatabasesAsync()
    {
        // Connect to the server's maintenance database - a database can't drop itself.
        await using var connection = new NpgsqlConnection($"{ServerConnectionString};Database=postgres");
        await connection.OpenAsync();

        var names = new List<string>();
        await using (var list = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE datname LIKE 'freight_integrationtests_%'", connection))
        await using (var reader = await list.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                names.Add(reader.GetString(0));
            }
        }

        foreach (var name in names)
        {
            // WITH (FORCE) closes any connection still left open to it, e.g. a psql session
            // used to inspect it. Names come from pg_database, not user input.
            await using var drop = new NpgsqlCommand($"DROP DATABASE \"{name}\" WITH (FORCE)", connection);
            await drop.ExecuteNonQueryAsync();
        }

        return names;
    }

    private FreightDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<FreightDbContext>().UseNpgsql(ConnectionString).Options);
}
