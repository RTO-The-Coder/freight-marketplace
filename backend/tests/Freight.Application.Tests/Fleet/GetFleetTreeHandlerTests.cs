using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetFleetTreeHandlerTests
{
    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static Driver SomeDriver(Guid companyId, string firstName = "Jane") =>
        Driver.Create(Guid.NewGuid(), firstName, "Doe", SomeRules(), companyId);

    private static Mock<IUnitOfWork> SetUp(Guid companyId, IReadOnlyList<Truck> companyTrucks, IReadOnlyList<Driver> companyDrivers)
    {
        var truckRepo = new Mock<ITruckRepository>();
        truckRepo.Setup(t => t.GetByTruckingCompanyIdAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync(companyTrucks);
        var driverRepo = new Mock<IDriverRepository>();
        driverRepo.Setup(d => d.GetByTruckingCompanyIdAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync(companyDrivers);
        var tripRepo = new Mock<ITripRepository>();
        tripRepo.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(truckRepo.Object);
        unitOfWork.SetupGet(u => u.Drivers).Returns(driverRepo.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(tripRepo.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task HandleAsync_QueriesTrucksAndDriversScopedToCompany()
    {
        var companyId = Guid.NewGuid();
        var unitOfWork = SetUp(companyId, [], []);

        var handler = new GetFleetTreeHandler(unitOfWork.Object);
        await handler.HandleAsync(new GetFleetTreeRequest(companyId));

        Mock.Get(unitOfWork.Object.Trucks).Verify(t => t.GetByTruckingCompanyIdAsync(companyId, It.IsAny<CancellationToken>()), Times.Once);
        Mock.Get(unitOfWork.Object.Drivers).Verify(d => d.GetByTruckingCompanyIdAsync(companyId, It.IsAny<CancellationToken>()), Times.Once);
        Mock.Get(unitOfWork.Object.Drivers).Verify(d => d.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_TruckWithDriverAssignment_MapsPerTruckStatusUsingItsOwnOpenTrip()
    {
        var companyId = Guid.NewGuid();
        var driver = SomeDriver(companyId);
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(companyId);
        truck.AssignDrivers(driver);
        var unitOfWork = SetUp(companyId, [truck], [driver]);

        var handler = new GetFleetTreeHandler(unitOfWork.Object);
        var response = await handler.HandleAsync(new GetFleetTreeRequest(companyId));

        var truckDto = Assert.Single(response.Trucks);
        Assert.Equal(TruckStatus.AtOffice, truckDto.Status);
        Assert.NotNull(truckDto.DriverAssignment);
        Assert.Equal(driver.Id, truckDto.DriverAssignment!.PrimaryDriver.DriverId);
        Assert.Empty(response.UnassignedDrivers);
    }

    [Fact]
    public async Task HandleAsync_UnassignedDrivers_ExcludesBothPrimaryAndSecondary()
    {
        var companyId = Guid.NewGuid();
        var primary = SomeDriver(companyId);
        var secondary = SomeDriver(companyId);
        var unassigned = SomeDriver(companyId);
        var teamTruck = Truck.Create(Guid.NewGuid(), "Truck-Team", TruckType.Refrigerated, TruckSize.Large);
        teamTruck.AssignToCompany(companyId);
        teamTruck.AssignDrivers(primary, secondary);
        var unitOfWork = SetUp(companyId, [teamTruck], [primary, secondary, unassigned]);

        var handler = new GetFleetTreeHandler(unitOfWork.Object);
        var response = await handler.HandleAsync(new GetFleetTreeRequest(companyId));

        var dto = Assert.Single(response.UnassignedDrivers);
        Assert.Equal(unassigned.Id, dto.DriverId);
    }

    [Fact]
    public async Task HandleAsync_Drivers_ListsEveryCompanyDriverWithItsAssignedTruck()
    {
        var companyId = Guid.NewGuid();
        var primary = SomeDriver(companyId, "Lena");
        var secondary = SomeDriver(companyId, "Jonas");
        var unassigned = SomeDriver(companyId, "Paul");
        var teamTruck = Truck.Create(Guid.NewGuid(), "Flatbed 1", TruckType.Flatbed, TruckSize.Large);
        teamTruck.AssignToCompany(companyId);
        teamTruck.AssignDrivers(primary, secondary);
        var unitOfWork = SetUp(companyId, [teamTruck], [primary, secondary, unassigned]);

        var handler = new GetFleetTreeHandler(unitOfWork.Object);
        var response = await handler.HandleAsync(new GetFleetTreeRequest(companyId));

        Assert.Equal(3, response.Drivers.Count);
        var lena = Assert.Single(response.Drivers, d => d.DriverId == primary.Id);
        Assert.Equal("Lena", lena.FirstName);
        Assert.Equal(teamTruck.Id, lena.AssignedTruckId);
        Assert.Equal("Flatbed 1", lena.AssignedTruckName);
        var jonas = Assert.Single(response.Drivers, d => d.DriverId == secondary.Id);
        Assert.Equal(teamTruck.Id, jonas.AssignedTruckId);
        var paul = Assert.Single(response.Drivers, d => d.DriverId == unassigned.Id);
        Assert.Null(paul.AssignedTruckId);
        Assert.Null(paul.AssignedTruckName);
    }

    [Fact]
    public async Task HandleAsync_DriverOfAnotherCompany_IsNotListed()
    {
        // The repository returns only this company's drivers, so another company's driver -
        // assigned or not - never appears in either list.
        var companyId = Guid.NewGuid();
        var otherCompanysDriver = SomeDriver(Guid.NewGuid());
        var unitOfWork = SetUp(companyId, [], []);
        Mock.Get(unitOfWork.Object.Drivers)
            .Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([otherCompanysDriver]);

        var handler = new GetFleetTreeHandler(unitOfWork.Object);
        var response = await handler.HandleAsync(new GetFleetTreeRequest(companyId));

        Assert.Empty(response.UnassignedDrivers);
        Assert.Empty(response.Drivers);
    }

    [Fact]
    public async Task HandleAsync_NoDriverAssignment_TruckDtoHasNullDriverAssignment()
    {
        var companyId = Guid.NewGuid();
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(companyId);
        var unitOfWork = SetUp(companyId, [truck], []);

        var handler = new GetFleetTreeHandler(unitOfWork.Object);
        var response = await handler.HandleAsync(new GetFleetTreeRequest(companyId));

        Assert.Null(Assert.Single(response.Trucks).DriverAssignment);
    }
}
