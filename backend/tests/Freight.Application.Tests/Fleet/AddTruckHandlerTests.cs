using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class AddTruckHandlerTests
{
    [Fact]
    public async Task AddTruckAsync_WithTruckingCompanyId_AssignsTruckToCompany()
    {
        var trucks = new Mock<ITruckRepository>();
        Truck? addedTruck = null;
        trucks.Setup(t => t.Add(It.IsAny<Truck>())).Callback<Truck>(t => addedTruck = t);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        var handler = new AddTruckHandler(unitOfWork.Object);
        var companyId = Guid.NewGuid();

        var response = await handler.AddTruckAsync(new AddTruckRequest("Truck 1", TruckType.Refrigerated, TruckSize.Medium, companyId));

        Assert.NotNull(addedTruck);
        Assert.Equal(companyId, addedTruck!.TruckingCompanyId);
        Assert.Equal(companyId, response.TruckingCompanyId);
        Assert.Equal(response.TruckId, addedTruck.Id);
        Assert.Equal(Capacity.ForTruckSize(TruckSize.Medium), response.TruckCapacity);
        Assert.False(response.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddTruckAsync_WithoutTruckingCompanyId_AddsUnassignedTruck_NeverCallsAssignToCompany()
    {
        var trucks = new Mock<ITruckRepository>();
        Truck? addedTruck = null;
        trucks.Setup(t => t.Add(It.IsAny<Truck>())).Callback<Truck>(t => addedTruck = t);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        var handler = new AddTruckHandler(unitOfWork.Object);

        var response = await handler.AddTruckAsync(new AddTruckRequest("Truck 1", TruckType.Refrigerated, TruckSize.Medium));

        Assert.NotNull(addedTruck);
        Assert.Null(addedTruck!.TruckingCompanyId);
        Assert.Null(response.TruckingCompanyId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddTruckAsync_BlankName_PropagatesDomainValidationUnwrapped_NeverSaves()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        var handler = new AddTruckHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.AddTruckAsync(new AddTruckRequest("", TruckType.Refrigerated, TruckSize.Medium)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
