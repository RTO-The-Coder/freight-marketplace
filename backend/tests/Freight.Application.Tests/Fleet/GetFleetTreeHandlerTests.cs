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

    private static Driver SomeDriver() => Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());

    private static Mock<IUnitOfWork> SetUp(Guid companyId, IReadOnlyList<Truck> companyTrucks, IReadOnlyList<Driver> allDrivers)
    {
        var truckRepo = new Mock<ITruckRepository>();
        truckRepo.Setup(t => t.GetByTruckingCompanyIdAsync(companyId, It.IsAny<CancellationToken>())).ReturnsAsync(companyTrucks);
        var driverRepo = new Mock<IDriverRepository>();
        driverRepo.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(allDrivers);
        var tripRepo = new Mock<ITripRepository>();
        tripRepo.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(truckRepo.Object);
        unitOfWork.SetupGet(u => u.Drivers).Returns(driverRepo.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(tripRepo.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task HandleAsync_QueriesTrucksScopedToCompany()
    {
        var companyId = Guid.NewGuid();
        var unitOfWork = SetUp(companyId, [], []);

        var handler = new GetFleetTreeHandler(unitOfWork.Object);
        await handler.HandleAsync(new GetFleetTreeRequest(companyId));

        Mock.Get(unitOfWork.Object.Trucks).Verify(t => t.GetByTruckingCompanyIdAsync(companyId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TruckWithDriverAssignment_MapsPerTruckStatusUsingItsOwnOpenTrip()
    {
        var companyId = Guid.NewGuid();
        var driver = SomeDriver();
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
    public async Task HandleAsync_UnassignedDrivers_ExcludesBothPrimaryAndSecondaryWithinScopedCompany()
    {
        var companyId = Guid.NewGuid();
        var primary = SomeDriver();
        var secondary = SomeDriver();
        var unassigned = SomeDriver();
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
    public async Task HandleAsync_DriverAssignedToTruckInAnotherCompany_IncorrectlyShowsAsUnassignedHere()
    {
        // Documents a real cross-company leak: UnassignedDrivers is computed from the
        // GLOBAL driver list minus only the drivers assigned within THIS company's trucks
        // (GetByTruckingCompanyIdAsync-scoped). A driver whose truck belongs to a
        // DIFFERENT company is not excluded, so they appear here as "unassigned" even
        // though they are actually assigned elsewhere.
        var companyId = Guid.NewGuid();
        var otherCompanyId = Guid.NewGuid();
        var driverInOtherCompany = SomeDriver();
        var truckInOtherCompany = Truck.Create(Guid.NewGuid(), "Truck-Other", TruckType.Refrigerated, TruckSize.Medium);
        truckInOtherCompany.AssignToCompany(otherCompanyId);
        truckInOtherCompany.AssignDrivers(driverInOtherCompany);

        // SetUp only returns trucks scoped to `companyId` - truckInOtherCompany is never in
        // that list, matching what GetByTruckingCompanyIdAsync would actually return.
        var unitOfWork = SetUp(companyId, [], [driverInOtherCompany]);

        var handler = new GetFleetTreeHandler(unitOfWork.Object);
        var response = await handler.HandleAsync(new GetFleetTreeRequest(companyId));

        var dto = Assert.Single(response.UnassignedDrivers);
        Assert.Equal(driverInOtherCompany.Id, dto.DriverId);
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
