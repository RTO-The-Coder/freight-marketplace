using Freight.Domain.Fleet;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Freight.Api.Tests.TestSupport;

/// <summary>
/// Boots the real API pipeline (routing, model binding, JSON, the exception-mapping
/// middleware, DI-registered handlers) against a real Postgres server - no mocking of EF
/// Core or the DB - but each test class gets its own throwaway database
/// ("freight_apitests_&lt;guid&gt;"), migrated fresh in InitializeAsync and dropped
/// entirely in DisposeAsync. This never touches row-level data in the shared dev database
/// ("freight_marketplace") or in any other test project's database, and needs no
/// truncate/delete-rows teardown logic - dropping a disposable, single-purpose database is
/// a different operation from deleting rows out of shared data.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    private const string ServerConnectionString =
        "Host=localhost;Port=5432;Username=freight;Password=freight_dev_password";

    private readonly string _databaseName = $"freight_apitests_{Guid.NewGuid():N}";

    public string ConnectionString => $"{ServerConnectionString};Database={_databaseName}";

    public FakeRoutingService RoutingService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FreightDb"] = ConnectionString
            });
        });

        // Replace the real OSRM-backed IRoutingService with a deterministic in-memory
        // fake - these are HTTP-pipeline tests, not routing-provider integration tests,
        // and the real one hits a public rate-limited third party over the network.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRoutingService>();
            services.AddSingleton<IRoutingService>(RoutingService);
        });
    }

    public async Task CreateDatabaseAsync()
    {
        await using var dbContext = new FreightDbContext(
            new DbContextOptionsBuilder<FreightDbContext>().UseNpgsql(ConnectionString).Options);
        await dbContext.Database.MigrateAsync();
    }

    public async Task DropDatabaseAsync()
    {
        await using var dbContext = new FreightDbContext(
            new DbContextOptionsBuilder<FreightDbContext>().UseNpgsql(ConnectionString).Options);
        await dbContext.Database.EnsureDeletedAsync();
    }

    /// <summary>
    /// There is no HTTP endpoint to create a trucking company (companies are seeded, not
    /// created through the API) - tests that need one seed it directly, the same way
    /// Freight.Application.Tests' integration scenarios seed via IUnitOfWork directly.
    /// </summary>
    public async Task<TruckingCompany> SeedTruckingCompanyAsync(string name = "Acme Trucking")
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), name, GeoLocation.Create(50.11, 8.68));

        await using var dbContext = new FreightDbContext(
            new DbContextOptionsBuilder<FreightDbContext>().UseNpgsql(ConnectionString).Options);
        dbContext.Set<TruckingCompany>().Add(company);
        await dbContext.SaveChangesAsync();

        return company;
    }

    /// <summary>
    /// There is likewise no HTTP endpoint to create a shipper.
    /// </summary>
    public async Task<Domain.Client.Shipper> SeedShipperAsync(string name = "Acme Shipping", string contactEmail = "contact@acme.com")
    {
        var shipper = Domain.Client.Shipper.Create(Guid.NewGuid(), name, contactEmail);

        await using var dbContext = new FreightDbContext(
            new DbContextOptionsBuilder<FreightDbContext>().UseNpgsql(ConnectionString).Options);
        dbContext.Set<Domain.Client.Shipper>().Add(shipper);
        await dbContext.SaveChangesAsync();

        return shipper;
    }
}
