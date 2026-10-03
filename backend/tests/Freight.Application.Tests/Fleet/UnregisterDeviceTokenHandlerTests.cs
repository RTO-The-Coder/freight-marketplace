using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class UnregisterDeviceTokenHandlerTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly DateTime RegisteredAt = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public Mock<IDeviceTokenRepository> DeviceTokens { get; } = new();
        public Mock<IDeviceTokenEncryptor> Encryptor { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public Harness()
        {
            // "enc(x)" decrypts back to "x".
            Encryptor.Setup(e => e.Decrypt(It.IsAny<string>()))
                .Returns((string encoded) => encoded["enc(".Length..^1]);
            UnitOfWork.SetupGet(u => u.DeviceTokens).Returns(DeviceTokens.Object);
        }

        public void HasRegistered(DeviceToken? deviceToken) =>
            DeviceTokens
                .Setup(d => d.GetByTruckingCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(deviceToken);

        public UnregisterDeviceTokenHandler NewHandler() => new(UnitOfWork.Object, Encryptor.Object);
    }

    [Fact]
    public async Task UnregisterAsync_ThisDevicesFid_RemovesTheRegistration()
    {
        var harness = new Harness();
        var registered = DeviceToken.Create(Guid.NewGuid(), CompanyId, "enc(fid-1)", RegisteredAt);
        harness.HasRegistered(registered);

        await harness.NewHandler().UnregisterAsync(new UnregisterDeviceTokenRequest(CompanyId, "fid-1"));

        harness.DeviceTokens.Verify(d => d.Remove(registered), Times.Once);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnregisterAsync_AnotherDeviceRegisteredSince_KeepsItsRegistration()
    {
        var harness = new Harness();
        harness.HasRegistered(DeviceToken.Create(Guid.NewGuid(), CompanyId, "enc(other-phone)", RegisteredAt));

        await harness.NewHandler().UnregisterAsync(new UnregisterDeviceTokenRequest(CompanyId, "fid-1"));

        harness.DeviceTokens.Verify(d => d.Remove(It.IsAny<DeviceToken>()), Times.Never);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnregisterAsync_NothingRegistered_DoesNothing()
    {
        var harness = new Harness();
        harness.HasRegistered(null);

        await harness.NewHandler().UnregisterAsync(new UnregisterDeviceTokenRequest(CompanyId, "fid-1"));

        harness.DeviceTokens.Verify(d => d.Remove(It.IsAny<DeviceToken>()), Times.Never);
        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnregisterAsync_BlankFid_ThrowsAndSavesNothing(string fid)
    {
        var harness = new Harness();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            harness.NewHandler().UnregisterAsync(new UnregisterDeviceTokenRequest(CompanyId, fid)));

        harness.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
