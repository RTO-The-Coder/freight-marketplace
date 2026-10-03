using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class RegisterDeviceTokenHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private sealed class Harness
    {
        public TruckingCompany Company { get; } =
            TruckingCompany.Create(Guid.NewGuid(), "Acme Trucking", GeoLocation.Create(52.52, 13.405));

        public Mock<ITruckingCompanyRepository> Companies { get; } = new();
        public Mock<IDeviceTokenRepository> DeviceTokens { get; } = new();
        public Mock<IDeviceTokenEncryptor> Encryptor { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public Harness()
        {
            Companies.Setup(c => c.GetByIdAsync(Company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Company);
            Encryptor.Setup(e => e.Encrypt(It.IsAny<string>())).Returns((string plain) => $"enc({plain})");
            UnitOfWork.SetupGet(u => u.TruckingCompanies).Returns(Companies.Object);
            UnitOfWork.SetupGet(u => u.DeviceTokens).Returns(DeviceTokens.Object);
        }

        public RegisterDeviceTokenHandler NewHandler() =>
            new(UnitOfWork.Object, Encryptor.Object, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task RegisterAsync_NoExistingDevice_AddsEncryptedFid()
    {
        var harness = new Harness();
        harness.DeviceTokens
            .Setup(d => d.GetByTruckingCompanyIdAsync(harness.Company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeviceToken?)null);
        DeviceToken? added = null;
        harness.DeviceTokens.Setup(d => d.Add(It.IsAny<DeviceToken>())).Callback<DeviceToken>(t => added = t);

        await harness.NewHandler().RegisterAsync(new RegisterDeviceTokenRequest(harness.Company.Id, "fid-1"));

        Assert.NotNull(added);
        Assert.Equal(harness.Company.Id, added!.TruckingCompanyId);
        Assert.Equal("enc(fid-1)", added.Fid);
        Assert.Equal(Now.UtcDateTime, added.RegisteredAt);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ExistingDevice_ReplacesItsFidInsteadOfAddingAnother()
    {
        var harness = new Harness();
        var existing = DeviceToken.Create(Guid.NewGuid(), harness.Company.Id, "enc(old-fid)", Now.UtcDateTime.AddDays(-3));
        harness.DeviceTokens
            .Setup(d => d.GetByTruckingCompanyIdAsync(harness.Company.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await harness.NewHandler().RegisterAsync(new RegisterDeviceTokenRequest(harness.Company.Id, "new-fid"));

        Assert.Equal("enc(new-fid)", existing.Fid);
        Assert.Equal(Now.UtcDateTime, existing.RegisteredAt);
        harness.DeviceTokens.Verify(d => d.Add(It.IsAny<DeviceToken>()), Times.Never);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_UnknownCompany_ThrowsAndSavesNothing()
    {
        var harness = new Harness();
        harness.Companies
            .Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TruckingCompany?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.NewHandler().RegisterAsync(new RegisterDeviceTokenRequest(Guid.NewGuid(), "fid")));

        harness.DeviceTokens.Verify(d => d.Add(It.IsAny<DeviceToken>()), Times.Never);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_BlankFid_ThrowsAndSavesNothing(string fid)
    {
        var harness = new Harness();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.NewHandler().RegisterAsync(new RegisterDeviceTokenRequest(harness.Company.Id, fid)));

        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
