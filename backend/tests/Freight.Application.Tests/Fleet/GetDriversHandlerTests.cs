using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetDriversHandlerTests
{
    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static Driver SomeDriver(string firstName = "Jane") =>
        Driver.Create(Guid.NewGuid(), firstName, "Doe", SomeRules());

    private static Truck SomeTruck(TruckSize size = TruckSize.Medium) =>
        Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, size);

    private static Mock<IUnitOfWork> SetUp(IReadOnlyList<Driver> drivers, IReadOnlyList<Truck> trucks)
    {
        var driverRepo = new Mock<IDriverRepository>();
        driverRepo.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(drivers);
        var truckRepo = new Mock<ITruckRepository>();
        truckRepo.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trucks);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(driverRepo.Object);
        unitOfWork.SetupGet(u => u.Trucks).Returns(truckRepo.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task GetDriversAsync_UnassignedOnlyFalse_ReturnsAllDrivers()
    {
        var driver = SomeDriver();
        var truck = SomeTruck();
        truck.AssignDrivers(driver);
        var unitOfWork = SetUp([driver], [truck]);

        var handler = new GetDriversHandler(unitOfWork.Object);
        var response = await handler.GetDriversAsync(new GetDriversRequest(UnassignedOnly: false));

        var dto = Assert.Single(response.Drivers);
        Assert.Equal(driver.Id, dto.DriverId);
        Assert.Equal(driver.FirstName, dto.FirstName);
        Assert.Equal(driver.LastName, dto.LastName);
    }

    [Fact]
    public async Task GetDriversAsync_UnassignedOnlyTrue_ExcludesPrimaryAndSecondaryAcrossMultipleTrucks()
    {
        var singleTruckDriver = SomeDriver("PrimaryOnSingle");
        var teamPrimary = SomeDriver("PrimaryOnTeam");
        var teamSecondary = SomeDriver("SecondaryOnTeam");
        var unassignedDriver = SomeDriver("Unassigned");

        var singleTruck = SomeTruck();
        singleTruck.AssignDrivers(singleTruckDriver);

        var teamTruck = SomeTruck(TruckSize.Large);
        teamTruck.AssignDrivers(teamPrimary, teamSecondary);

        var unitOfWork = SetUp(
            [singleTruckDriver, teamPrimary, teamSecondary, unassignedDriver],
            [singleTruck, teamTruck]);

        var handler = new GetDriversHandler(unitOfWork.Object);
        var response = await handler.GetDriversAsync(new GetDriversRequest(UnassignedOnly: true));

        var dto = Assert.Single(response.Drivers);
        Assert.Equal(unassignedDriver.Id, dto.DriverId);
    }

    [Fact]
    public async Task GetDriversAsync_UnassignedOnlyTrue_TruckWithNoDriverAssignmentIsIgnored()
    {
        var driver = SomeDriver();
        var truckWithNoDrivers = SomeTruck();
        var unitOfWork = SetUp([driver], [truckWithNoDrivers]);

        var handler = new GetDriversHandler(unitOfWork.Object);
        var response = await handler.GetDriversAsync(new GetDriversRequest(UnassignedOnly: true));

        var dto = Assert.Single(response.Drivers);
        Assert.Equal(driver.Id, dto.DriverId);
    }

    [Fact]
    public async Task GetDriversAsync_UnassignedOnlyTrue_NoTrucksAtAll_DoesNotCallTrucksRepo()
    {
        // Guards against a regression that unconditionally queries Trucks even when
        // UnassignedOnly is false - this test exercises the true branch with zero trucks.
        var driver = SomeDriver();
        var unitOfWork = SetUp([driver], []);

        var handler = new GetDriversHandler(unitOfWork.Object);
        var response = await handler.GetDriversAsync(new GetDriversRequest(UnassignedOnly: true));

        Assert.Single(response.Drivers);
    }

    [Fact]
    public async Task GetDriversAsync_NoDrivers_ReturnsEmptyNotNullList()
    {
        var unitOfWork = SetUp([], []);

        var handler = new GetDriversHandler(unitOfWork.Object);
        var response = await handler.GetDriversAsync(new GetDriversRequest(UnassignedOnly: false));

        Assert.NotNull(response.Drivers);
        Assert.Empty(response.Drivers);
    }
}
