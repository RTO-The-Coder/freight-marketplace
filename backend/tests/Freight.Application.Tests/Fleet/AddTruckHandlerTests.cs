using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class AddTruckHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithTruckingCompanyId_AddsTruckAssignedToCompanyAndSaves()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        Truck? addedTruck = null;
        trucks.Setup(t => t.Add(It.IsAny<Truck>())).Callback<Truck>(t => addedTruck = t);

        var handler = new AddTruckHandler(unitOfWork.Object);
        var companyId = Guid.NewGuid();

        var response = await handler.AddTruckAsync(
            new AddTruckRequest("Truck 1", TruckType.BoxVan, TruckSize.Medium, companyId));

        Assert.NotNull(addedTruck);
        Assert.Equal(companyId, addedTruck!.TruckingCompanyId);
        Assert.Equal(response.TruckId, addedTruck.Id);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithoutTruckingCompanyId_AddsUnassignedTruckAndSaves()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        Truck? addedTruck = null;
        trucks.Setup(t => t.Add(It.IsAny<Truck>())).Callback<Truck>(t => addedTruck = t);

        var handler = new AddTruckHandler(unitOfWork.Object);

        var response = await handler.AddTruckAsync(new AddTruckRequest("Truck 1", TruckType.BoxVan, TruckSize.Medium));

        Assert.NotNull(addedTruck);
        Assert.Null(addedTruck!.TruckingCompanyId);
        Assert.Equal(response.TruckId, addedTruck.Id);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
