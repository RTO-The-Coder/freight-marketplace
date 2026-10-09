using FirebaseAdmin;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.Notifications;
using Freight.Domain.Simulation.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Infrastructure.Notifications;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging.Abstractions;

namespace Freight.Infrastructure.Tests;

/// <summary>
/// Only the paths that never reach Firebase are covered here - an actual send needs a real
/// Firebase project and device, and is checked end to end instead (see
/// docs/design/mobile-push-notifications-handoff.md). The FirebaseApp uses a dummy access
/// token, so any path that did call FCM would fail rather than send.
/// </summary>
public sealed class FcmNotificationSenderTests : IDisposable
{
    private readonly FirebaseApp _firebaseApp = FirebaseApp.Create(
        new AppOptions
        {
            Credential = GoogleCredential.FromAccessToken("test-access-token"),
            ProjectId = "freight-test",
        },
        $"fcm-sender-tests-{Guid.NewGuid()}");

    public void Dispose() => _firebaseApp.Delete();

    private static ShipmentNotificationSummary SomeSummary() => new(
        Guid.NewGuid(),
        GeoLocation.Create(52.52, 13.405),
        TruckType.Refrigerated,
        TimeWindow.Create(new DateTime(2026, 1, 1, 9, 0, 0), new DateTime(2026, 1, 1, 11, 0, 0)));

    [Fact]
    public async Task NotifyAllCompaniesAsync_NoRegisteredDevices_DoesNothing()
    {
        var encryptor = new RecordingEncryptor();
        var sender = new FcmNotificationSender(
            new DeviceTokensOnlyUnitOfWork([]), encryptor, _firebaseApp, NullLogger<FcmNotificationSender>.Instance);

        await sender.NotifyAllCompaniesAsync(SomeSummary());

        Assert.Empty(encryptor.Decrypted);
    }

    [Fact]
    public async Task NotifyAllCompaniesAsync_NoFidCanBeDecrypted_SkipsThemWithoutThrowing()
    {
        var devices = new[]
        {
            DeviceToken.Create(Guid.NewGuid(), Guid.NewGuid(), "corrupt-1", DateTime.UtcNow),
            DeviceToken.Create(Guid.NewGuid(), Guid.NewGuid(), "corrupt-2", DateTime.UtcNow),
        };
        var encryptor = new RecordingEncryptor(throwOnDecrypt: true);
        var sender = new FcmNotificationSender(
            new DeviceTokensOnlyUnitOfWork(devices), encryptor, _firebaseApp, NullLogger<FcmNotificationSender>.Instance);

        await sender.NotifyAllCompaniesAsync(SomeSummary());

        Assert.Equal(["corrupt-1", "corrupt-2"], encryptor.Decrypted);
    }

    [Fact]
    public async Task NotifyCompanyAsync_CompanyHasNoRegisteredDevice_DoesNothing()
    {
        var otherCompanysDevice = DeviceToken.Create(Guid.NewGuid(), Guid.NewGuid(), "other-fid", DateTime.UtcNow);
        var encryptor = new RecordingEncryptor();
        var sender = new FcmNotificationSender(
            new DeviceTokensOnlyUnitOfWork([otherCompanysDevice]), encryptor, _firebaseApp, NullLogger<FcmNotificationSender>.Instance);

        await sender.NotifyCompanyAsync(Guid.NewGuid(), SomeSummary() with { IsDirect = true });

        Assert.Empty(encryptor.Decrypted);
    }

    [Fact]
    public async Task NotifyCompanyAsync_OnlyThatCompanysDeviceIsUsed_UndecryptableIsSkippedWithoutThrowing()
    {
        var companyId = Guid.NewGuid();
        var devices = new[]
        {
            DeviceToken.Create(Guid.NewGuid(), Guid.NewGuid(), "other-fid", DateTime.UtcNow),
            DeviceToken.Create(Guid.NewGuid(), companyId, "chosen-corrupt", DateTime.UtcNow),
        };
        var encryptor = new RecordingEncryptor(throwOnDecrypt: true);
        var sender = new FcmNotificationSender(
            new DeviceTokensOnlyUnitOfWork(devices), encryptor, _firebaseApp, NullLogger<FcmNotificationSender>.Instance);

        await sender.NotifyCompanyAsync(companyId, SomeSummary() with { IsDirect = true });

        Assert.Equal(["chosen-corrupt"], encryptor.Decrypted);
    }

    private sealed class RecordingEncryptor(bool throwOnDecrypt = false) : IDeviceTokenEncryptor
    {
        public List<string> Decrypted { get; } = [];

        public string Encrypt(string plaintext) => plaintext;

        public string Decrypt(string encoded)
        {
            Decrypted.Add(encoded);
            return throwOnDecrypt
                ? throw new System.Security.Cryptography.CryptographicException("bad ciphertext")
                : encoded;
        }
    }

    private sealed class DeviceTokensOnlyUnitOfWork(IReadOnlyList<DeviceToken> devices) : IUnitOfWork
    {
        public IDeviceTokenRepository DeviceTokens { get; } = new ListDeviceTokenRepository(devices);

        public ITruckingCompanyRepository TruckingCompanies => throw new NotSupportedException();
        public IShipperRepository Shippers => throw new NotSupportedException();
        public ITruckRepository Trucks => throw new NotSupportedException();
        public ITripRepository Trips => throw new NotSupportedException();
        public IDriverRepository Drivers => throw new NotSupportedException();
        public IShipmentRepository Shipments => throw new NotSupportedException();
        public IShipmentOfferRepository ShipmentOffers => throw new NotSupportedException();
        public ISimulationClockRepository SimulationClock => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ListDeviceTokenRepository(IReadOnlyList<DeviceToken> devices) : IDeviceTokenRepository
    {
        public Task<IReadOnlyList<DeviceToken>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(devices);

        public Task<DeviceToken?> GetByTruckingCompanyIdAsync(Guid truckingCompanyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(devices.FirstOrDefault(device => device.TruckingCompanyId == truckingCompanyId));

        public Task<DeviceToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public void Add(DeviceToken entity) => throw new NotSupportedException();

        public void Remove(DeviceToken entity) => throw new NotSupportedException();
    }
}
