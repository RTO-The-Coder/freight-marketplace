using Freight.Domain.Client;
using Freight.Domain.Client.Enums;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Persistence;
using Freight.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Tests;

public sealed class ShipmentOfferRoundTripTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=freight_marketplace;Username=freight;Password=freight_dev_password";

    private static readonly DateTime CreatedAt = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    // Random per run, so these rows never collide with real data in the shared dev database -
    // and are deleted again in DisposeAsync.
    private readonly Guid _shipmentId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();

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
        await dbContext.Set<ShipmentOffer>().Where(o => o.ShipmentId == _shipmentId).ExecuteDeleteAsync();
        await dbContext.Set<Shipment>().Where(s => s.Id == _shipmentId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task SavedOffer_IsFoundByShipmentAndCompanyWithSameValues()
    {
        var limitAt = CreatedAt.AddMinutes(10);
        var offer = ShipmentOffer.Create(_shipmentId, _companyId, Guid.NewGuid(), 1, 3, 12.5, 850.75m, limitAt, CreatedAt);
        offer.Accept(CreatedAt.AddMinutes(5), CreatedAt.AddHours(2));
        await using (var dbContext = new FreightDbContext(Options()))
        {
            new ShipmentOfferRepository(dbContext).Add(offer);
            await dbContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var repository = new ShipmentOfferRepository(readContext);
        var loaded = Assert.Single(await repository.GetByShipmentIdAsync(_shipmentId));

        Assert.Equal(offer.Id, loaded.Id);
        Assert.Equal(_companyId, loaded.TruckingCompanyId);
        Assert.Equal(offer.TruckId, loaded.TruckId);
        Assert.Equal(1, loaded.PickupInsertIndex);
        Assert.Equal(3, loaded.DeliveryInsertIndex);
        Assert.Equal(12.5, loaded.AddedDistanceKm);
        Assert.Equal(850.75m, loaded.PriceEur);
        Assert.Equal(limitAt, loaded.LimitAt);
        Assert.Equal(CreatedAt, loaded.CreatedAt);
        Assert.Equal(ShipmentOfferStatus.Accepted, loaded.Status);

        Assert.Contains(await repository.GetByCompanyIdAsync(_companyId), o => o.Id == offer.Id);
        Assert.Contains(await repository.GetByShipmentIdsAsync([_shipmentId, Guid.NewGuid()]), o => o.Id == offer.Id);
        Assert.Empty(await repository.GetByShipmentIdsAsync([]));
    }

    [Fact]
    public async Task SavedOffer_WithoutLimit_LoadsNullLimit()
    {
        var offer = ShipmentOffer.Create(_shipmentId, _companyId, Guid.NewGuid(), 0, 1, 0, 100m, null, CreatedAt);
        await using (var dbContext = new FreightDbContext(Options()))
        {
            dbContext.Add(offer);
            await dbContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var loaded = await new ShipmentOfferRepository(readContext).GetByIdAsync(offer.Id);

        Assert.NotNull(loaded);
        Assert.Null(loaded!.LimitAt);
        Assert.Equal(ShipmentOfferStatus.Pending, loaded.Status);
    }

    [Fact]
    public async Task DirectShipment_IsDirectAndCompanySurviveRoundTrip()
    {
        var shipment = Shipment.Book(
            _shipmentId, Guid.NewGuid(),
            GeoLocation.Create(52.52, 13.405), GeoLocation.Create(48.1351, 11.582),
            Capacity.Create(100, 1), TruckType.Refrigerated,
            TimeWindow.Create(CreatedAt.AddHours(1), CreatedAt.AddHours(3)),
            TimeWindow.Create(CreatedAt.AddHours(5), CreatedAt.AddHours(7)),
            CreatedAt,
            _companyId);
        await using (var dbContext = new FreightDbContext(Options()))
        {
            new ShipmentRepository(dbContext).Add(shipment);
            await dbContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var loaded = await new ShipmentRepository(readContext).GetByIdAsync(_shipmentId);

        Assert.NotNull(loaded);
        Assert.True(loaded!.IsDirect);
        Assert.Equal(_companyId, loaded.TruckingCompanyId);
        Assert.Equal(CreatedAt.AddHours(2), loaded.OfferDeadline);
    }
}
