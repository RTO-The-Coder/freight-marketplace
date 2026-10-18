using Freight.Domain.Client;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Persistence;
using Freight.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Tests;

/// <summary>
/// Place names and the shipper address book survive a save and a fresh load (ADR 0013): the
/// owned ShipperAddresses table, an address added to an already-saved shipper, and the place-name
/// columns on companies and shipments.
/// </summary>
public class TruckingCompanyAndShipperRoundTripTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=freight_marketplace;Username=freight;Password=freight_dev_password";

    private static readonly DateTime Now = new(2026, 8, 1, 5, 0, 0, DateTimeKind.Utc);

    // Random per run, so these rows never collide with real data in the shared dev database -
    // and are deleted again in DisposeAsync.
    private readonly Guid _shipperId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _shipmentId = Guid.NewGuid();

    private static DbContextOptions<FreightDbContext> Options() =>
        new DbContextOptionsBuilder<FreightDbContext>().UseNpgsql(ConnectionString).Options;

    public async Task InitializeAsync()
    {
        await using var dbContext = new FreightDbContext(Options());
        await dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var dbContext = new FreightDbContext(Options());
        await dbContext.Set<Shipment>().Where(s => s.Id == _shipmentId).ExecuteDeleteAsync();
        // Deleting the shipper cascades to its saved addresses.
        await dbContext.Set<Shipper>().Where(s => s.Id == _shipperId).ExecuteDeleteAsync();
        await dbContext.Set<TruckingCompany>().Where(c => c.Id == _companyId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task ShipperAddressBook_SavedAndExtendedLater_LoadsEveryAddressNewestFirst()
    {
        var shipper = Shipper.Create(_shipperId, "Acme Shipping", "contact@acme.com");
        shipper.AddAddress("Depot", "80803 Schwabing, Munich", GeoLocation.Create(48.1626, 11.5868), Now);
        await using (var dbContext = new FreightDbContext(Options()))
        {
            new ShipperRepository(dbContext).Add(shipper);
            await dbContext.SaveChangesAsync();
        }

        // Add to the address book of a shipper loaded from the database - a new owned row.
        await using (var dbContext = new FreightDbContext(Options()))
        {
            var loaded = await new ShipperRepository(dbContext).GetByIdAsync(_shipperId);
            loaded!.AddAddress("Main warehouse", "1100 Favoriten, Vienna", GeoLocation.Create(48.155, 16.382), Now.AddHours(1));
            await dbContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var reloaded = await new ShipperRepository(readContext).GetByIdAsync(_shipperId);

        Assert.NotNull(reloaded);
        Assert.Collection(
            reloaded.Addresses,
            newest =>
            {
                Assert.Equal("Main warehouse", newest.Label);
                Assert.Equal("1100 Favoriten, Vienna", newest.AreaName);
                Assert.Equal(GeoLocation.Create(48.155, 16.382), newest.Location);
                Assert.Equal(Now.AddHours(1), newest.CreatedAt);
            },
            oldest => Assert.Equal("Depot", oldest.Label));
    }

    [Fact]
    public async Task TruckingCompany_OfficePlaceName_RoundTrips()
    {
        await using (var dbContext = new FreightDbContext(Options()))
        {
            new TruckingCompanyRepository(dbContext).Add(
                TruckingCompany.Create(_companyId, "Acme Trucking", GeoLocation.Create(50.9540, 6.9200), "50825 Ehrenfeld, Cologne"));
            await dbContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var loaded = await new TruckingCompanyRepository(readContext).GetByIdAsync(_companyId);

        Assert.Equal("50825 Ehrenfeld, Cologne", loaded!.OfficePlaceName);
    }

    [Fact]
    public async Task Shipment_PlaceNames_RoundTripAndAreFoundById()
    {
        var shipment = Shipment.Book(
            _shipmentId, _shipperId,
            GeoLocation.Create(48.155, 16.382), "Main warehouse — 1100 Favoriten, Vienna",
            GeoLocation.Create(48.1626, 11.5868), "Depot — 80803 Schwabing, Munich",
            Capacity.Create(1000, 10), TruckType.Flatbed,
            TimeWindow.Create(Now.AddHours(2), Now.AddHours(4)),
            TimeWindow.Create(Now.AddHours(10), Now.AddHours(14)),
            Now);
        await using (var dbContext = new FreightDbContext(Options()))
        {
            new ShipmentRepository(dbContext).Add(shipment);
            await dbContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var loaded = Assert.Single(await new ShipmentRepository(readContext).GetByIdsAsync([_shipmentId, Guid.NewGuid()]));

        Assert.Equal("Main warehouse — 1100 Favoriten, Vienna", loaded.PickupPlaceName);
        Assert.Equal("Depot — 80803 Schwabing, Munich", loaded.DeliveryPlaceName);
        Assert.Empty(await new ShipmentRepository(readContext).GetByIdsAsync([]));
    }
}
