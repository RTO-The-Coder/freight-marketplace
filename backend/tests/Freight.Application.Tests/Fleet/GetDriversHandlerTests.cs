using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetDriversHandlerTests
{
    private static Driver NewDriver(string firstName) =>
        Driver.Create(
            firstName,
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
    public async Task HandleAsync_NotUnassignedOnly_ReturnsAllDriversWithoutQueryingTrucks()
    {
        var (unitOfWork, trucks, drivers) = NewMocks();
        var driver1 = NewDriver("Jane");
        var driver2 = NewDriver("John");

        drivers.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([driver1, driver2]);

        var handler = new GetDriversHandler(unitOfWork.Object);

        var response = await handler.HandleAsync(new GetDriversRequest(UnassignedOnly: false));

        Assert.Equal(2, response.Drivers.Count);
        trucks.Verify(t => t.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UnassignedOnly_ExcludesDriversReferencedByAnyTruck()
    {
        var (unitOfWork, trucks, drivers) = NewMocks();
        var assignedPrimary = NewDriver("Primary");
        var assignedSecondary = NewDriver("Secondary");
        var unassignedDriver = NewDriver("Free");

        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Large);
        truck.AssignDrivers(assignedPrimary, assignedSecondary);

        drivers.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([assignedPrimary, assignedSecondary, unassignedDriver]);
        trucks.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([truck]);

        var handler = new GetDriversHandler(unitOfWork.Object);

        var response = await handler.HandleAsync(new GetDriversRequest(UnassignedOnly: true));

        var dto = Assert.Single(response.Drivers);
        Assert.Equal(unassignedDriver.Id, dto.DriverId);
    }
}
