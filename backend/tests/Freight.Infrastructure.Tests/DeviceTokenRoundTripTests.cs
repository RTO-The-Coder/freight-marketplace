using Freight.Domain.Fleet;
using Freight.Infrastructure.Persistence;
using Freight.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Freight.Infrastructure.Tests;

public sealed class DeviceTokenRoundTripTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=freight_marketplace;Username=freight;Password=freight_dev_password";

    private static readonly DateTime RegisteredAt = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    // Random per run, so these rows never collide with real registrations in the shared dev
    // database - and are deleted again in DisposeAsync.
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
        await dbContext.Set<DeviceToken>().Where(t => t.TruckingCompanyId == _companyId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task SavedDevice_IsFoundByCompanyIdWithSameValues()
    {
        var deviceToken = DeviceToken.Create(Guid.NewGuid(), _companyId, "encrypted-fid", RegisteredAt);
        await using (var dbContext = new FreightDbContext(Options()))
        {
            new DeviceTokenRepository(dbContext).Add(deviceToken);
            await dbContext.SaveChangesAsync();
        }

        await using var readContext = new FreightDbContext(Options());
        var loaded = await new DeviceTokenRepository(readContext).GetByTruckingCompanyIdAsync(_companyId);

        Assert.NotNull(loaded);
        Assert.Equal(deviceToken.Id, loaded!.Id);
        Assert.Equal("encrypted-fid", loaded.Fid);
        Assert.Equal(RegisteredAt, loaded.RegisteredAt);
    }

    [Fact]
    public async Task SecondDeviceForSameCompany_IsRejectedByUniqueIndex()
    {
        await using (var dbContext = new FreightDbContext(Options()))
        {
            dbContext.Add(DeviceToken.Create(Guid.NewGuid(), _companyId, "fid-1", RegisteredAt));
            await dbContext.SaveChangesAsync();
        }

        await using var secondContext = new FreightDbContext(Options());
        secondContext.Add(DeviceToken.Create(Guid.NewGuid(), _companyId, "fid-2", RegisteredAt));

        await Assert.ThrowsAsync<DbUpdateException>(() => secondContext.SaveChangesAsync());
    }

    [Fact]
    public async Task UnknownCompany_ReturnsNull()
    {
        await using var dbContext = new FreightDbContext(Options());

        Assert.Null(await new DeviceTokenRepository(dbContext).GetByTruckingCompanyIdAsync(_companyId));
    }
}
