using Freight.Application.Shipments;
using Freight.Domain.Common;
using Freight.Domain.Client;
using Moq;
using ShipperAggregate = Freight.Domain.Client.Shipper;

namespace Freight.Application.Tests.Shipments;

public sealed class GetShippersHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsAllShippers()
    {
        var shippers = new Mock<IShipperRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Shippers).Returns(shippers.Object);

        var shipper1 = ShipperAggregate.Create(Guid.NewGuid(), "Acme Cargo", "ops@acme.example.com");
        var shipper2 = ShipperAggregate.Create(Guid.NewGuid(), "Globex Freight", "shipping@globex.example.com");
        shippers.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([shipper1, shipper2]);

        var handler = new GetShippersHandler(unitOfWork.Object);

        var response = await handler.HandleAsync();

        Assert.Equal(2, response.Shippers.Count);
        Assert.Contains(response.Shippers, s => s.ShipperId == shipper1.Id && s.Name == shipper1.Name);
        Assert.Contains(response.Shippers, s => s.ShipperId == shipper2.Id && s.Name == shipper2.Name);
    }
}
