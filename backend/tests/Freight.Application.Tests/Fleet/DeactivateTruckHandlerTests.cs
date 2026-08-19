using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class DeactivateTruckHandlerTests
{
    [Fact]
    public async Task HandleAsync_ActiveTruck_DeactivatesAndSaves()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        truck.AssignToCompany(Guid.NewGuid());
        truck.Activate();
        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);

        var handler = new DeactivateTruckHandler(unitOfWork.Object);

        await handler.HandleAsync(new DeactivateTruckRequest(truck.Id));

        Assert.False(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_UnknownTruckId_Throws()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Truck?)null);

        var handler = new DeactivateTruckHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new DeactivateTruckRequest(Guid.NewGuid())));
    }
}
