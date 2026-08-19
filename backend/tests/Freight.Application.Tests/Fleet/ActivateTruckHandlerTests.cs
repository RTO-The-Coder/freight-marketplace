using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class ActivateTruckHandlerTests
{
    [Fact]
    public async Task HandleAsync_TruckAssignedToCompany_ActivatesAndSaves()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        truck.AssignToCompany(Guid.NewGuid());
        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);

        var handler = new ActivateTruckHandler(unitOfWork.Object);

        await handler.HandleAsync(new ActivateTruckRequest(truck.Id));

        Assert.True(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TruckWithoutCompany_ThrowsAndDoesNotSave()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);

        var handler = new ActivateTruckHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new ActivateTruckRequest(truck.Id)));

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
