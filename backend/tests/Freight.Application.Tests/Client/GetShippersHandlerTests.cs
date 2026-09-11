using Freight.Application.Client;
using Freight.Domain.Client;
using Freight.Domain.Common;
using Freight.Domain.Client.Abstractions;
using Moq;

namespace Freight.Application.Tests.Client;

public sealed class GetShippersHandlerTests
{
    [Fact]
    public async Task GetShippersAsync_MapsEveryFieldCorrectly()
    {
        var shipper = Shipper.Create(Guid.NewGuid(), "Acme Shipping", "contact@acme.com");
        var shippers = new Mock<IShipperRepository>();
        shippers.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([shipper]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shippers).Returns(shippers.Object);

        var handler = new GetShippersHandler(unitOfWork.Object);
        var response = await handler.GetShippersAsync();

        var dto = Assert.Single(response.Shippers);
        Assert.Equal(shipper.Id, dto.ShipperId);
        Assert.Equal(shipper.Name, dto.Name);
        Assert.Equal(shipper.ContactEmail, dto.ContactEmail);
    }

    [Fact]
    public async Task GetShippersAsync_NoShippers_ReturnsEmptyNotNullList()
    {
        var shippers = new Mock<IShipperRepository>();
        shippers.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shippers).Returns(shippers.Object);

        var handler = new GetShippersHandler(unitOfWork.Object);
        var response = await handler.GetShippersAsync();

        Assert.NotNull(response.Shippers);
        Assert.Empty(response.Shippers);
    }

    [Fact]
    public async Task GetShippersAsync_MultipleShippers_PreservesRepoOrder()
    {
        var first = Shipper.Create(Guid.NewGuid(), "First", "first@acme.com");
        var second = Shipper.Create(Guid.NewGuid(), "Second", "second@acme.com");
        var shippers = new Mock<IShipperRepository>();
        shippers.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shippers).Returns(shippers.Object);

        var handler = new GetShippersHandler(unitOfWork.Object);
        var response = await handler.GetShippersAsync();

        Assert.Equal([first.Id, second.Id], response.Shippers.Select(s => s.ShipperId));
    }
}
