using Freight.Application.Client;
using Freight.Domain.Client;
using Freight.Domain.Client.Abstractions;
using Freight.Domain.Common;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Client;

public sealed class ShipperAddressHandlersTests
{
    private static readonly DateTime SimNow = new(2026, 8, 1, 5, 0, 0);

    private static Mock<IUnitOfWork> SetUp(Shipper? shipper)
    {
        var shippers = new Mock<IShipperRepository>();
        shippers.Setup(s => s.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(shipper);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shippers).Returns(shippers.Object);
        FakeSimulationClock.SetUp(unitOfWork, SimNow);
        return unitOfWork;
    }

    [Fact]
    public async Task AddShipperAddressAsync_SavesTheAddressAtSimulationTimeAndReturnsIt()
    {
        var shipper = Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        var unitOfWork = SetUp(shipper);
        var handler = new AddShipperAddressHandler(unitOfWork.Object, TimeProvider.System);

        var dto = await handler.AddShipperAddressAsync(new AddShipperAddressRequest(
            shipper.Id, "Main warehouse", "1100 Favoriten, Vienna", GeoLocation.Create(48.155, 16.382)));

        var saved = Assert.Single(shipper.Addresses);
        Assert.Equal(saved.Id, dto.AddressId);
        Assert.Equal("Main warehouse", dto.Label);
        Assert.Equal("1100 Favoriten, Vienna", dto.AreaName);
        Assert.Equal("Main warehouse — 1100 Favoriten, Vienna", dto.PlaceName);
        Assert.Equal(48.155, dto.Latitude);
        Assert.Equal(16.382, dto.Longitude);
        Assert.Equal(SimNow, dto.CreatedAt);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddShipperAddressAsync_UnknownShipper_ThrowsAndSavesNothing()
    {
        var unitOfWork = SetUp(null);
        var handler = new AddShipperAddressHandler(unitOfWork.Object, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.AddShipperAddressAsync(new AddShipperAddressRequest(
            Guid.NewGuid(), "Main warehouse", "1100 Favoriten, Vienna", GeoLocation.Create(48.155, 16.382))));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddShipperAddressAsync_BlankLabel_ThrowsAndSavesNothing()
    {
        var shipper = Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        var unitOfWork = SetUp(shipper);
        var handler = new AddShipperAddressHandler(unitOfWork.Object, TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.AddShipperAddressAsync(new AddShipperAddressRequest(
            shipper.Id, " ", "1100 Favoriten, Vienna", GeoLocation.Create(48.155, 16.382))));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetShipperAddressesAsync_ReturnsTheAddressBookNewestFirst()
    {
        var shipper = Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        shipper.AddAddress("Depot", "80803 Schwabing, Munich", GeoLocation.Create(48.16, 11.58), SimNow);
        shipper.AddAddress("Warehouse", "1100 Favoriten, Vienna", GeoLocation.Create(48.155, 16.382), SimNow.AddHours(1));
        var handler = new GetShipperAddressesHandler(SetUp(shipper).Object);

        var response = await handler.GetShipperAddressesAsync(new GetShipperAddressesRequest(shipper.Id));

        Assert.Equal(["Warehouse", "Depot"], response.Addresses.Select(a => a.Label));
    }

    [Fact]
    public async Task GetShipperAddressesAsync_UnknownShipper_Throws()
    {
        var handler = new GetShipperAddressesHandler(SetUp(null).Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GetShipperAddressesAsync(new GetShipperAddressesRequest(Guid.NewGuid())));
    }
}
