using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class AssignDriversHandlerTests
{
    private static Driver NewDriver() =>
        Driver.Create(
            "Jane",
            "Doe",
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));

    private static (Mock<IUnitOfWork> UnitOfWork, Mock<ITruckRepository> Trucks, Mock<IDriverRepository> Drivers) NewMocks()
    {
        var trucks = new Mock<ITruckRepository>();
        var drivers = new Mock<IDriverRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);
        return (unitOfWork, trucks, drivers);
    }

    [Fact]
    public async Task HandleAsync_SingleDriverOnMediumTruck_AssignsAndSaves()
    {
        var (unitOfWork, trucks, drivers) = NewMocks();
        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Medium);
        var primary = NewDriver();

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        drivers.Setup(d => d.GetByIdAsync(primary.Id, It.IsAny<CancellationToken>())).ReturnsAsync(primary);

        var handler = new AssignDriversHandler(unitOfWork.Object);

        await handler.AssignDrivers(new AssignDriversRequest(truck.Id, primary.Id, SecondaryDriverId: null));

        Assert.Equal(primary.Id, truck.DriverAssignment!.PrimaryDriver.Id);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SecondDriverOnMediumTruck_ThrowsAndDoesNotSave()
    {
        var (unitOfWork, trucks, drivers) = NewMocks();
        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Medium);
        var primary = NewDriver();
        var secondary = NewDriver();

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        drivers.Setup(d => d.GetByIdAsync(primary.Id, It.IsAny<CancellationToken>())).ReturnsAsync(primary);
        drivers.Setup(d => d.GetByIdAsync(secondary.Id, It.IsAny<CancellationToken>())).ReturnsAsync(secondary);

        var handler = new AssignDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignDrivers(new AssignDriversRequest(truck.Id, primary.Id, secondary.Id)));

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UnknownTruckId_Throws()
    {
        var (unitOfWork, trucks, _) = NewMocks();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Truck?)null);

        var handler = new AssignDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignDrivers(new AssignDriversRequest(Guid.NewGuid(), Guid.NewGuid(), null)));
    }
}
