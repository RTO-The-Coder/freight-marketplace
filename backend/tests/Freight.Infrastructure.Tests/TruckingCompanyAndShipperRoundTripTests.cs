using Freight.Domain.Fleet;
using Freight.Domain.Shipment;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Tests;

public class TruckingCompanyAndShipperRoundTripTests : IAsyncLifetime
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
}
