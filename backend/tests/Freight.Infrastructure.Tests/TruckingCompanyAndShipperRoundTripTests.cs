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

    [Fact]
    public async Task TruckingCompany_PersistedAndReloaded_RoundTripsOfficeLocation()
    {
        var company = TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", GeoLocation.Create(52.5200, 13.4050));

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<TruckingCompany>().Add(company);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await readContext.Set<TruckingCompany>().FirstAsync(c => c.Id == company.Id);

        Assert.Equal(company.Name, reloaded.Name);
        Assert.Equal(company.OfficeLocation.Latitude, reloaded.OfficeLocation.Latitude, precision: 6);
        Assert.Equal(company.OfficeLocation.Longitude, reloaded.OfficeLocation.Longitude, precision: 6);
    }

    [Fact]
    public async Task Shipper_PersistedAndReloaded_RoundTripsContactEmail()
    {
        var shipper = Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.example");

        await using (var writeContext = new FreightDbContext(Options()))
        {
            writeContext.Set<Shipper>().Add(shipper);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await readContext.Set<Shipper>().FirstAsync(s => s.Id == shipper.Id);

        Assert.Equal(shipper.Name, reloaded.Name);
        Assert.Equal(shipper.ContactEmail, reloaded.ContactEmail);
    }
}
