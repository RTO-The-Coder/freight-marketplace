using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class SetDriverCompanyHandlerTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();

    private static Driver SomeDriver(Guid? companyId = null) =>
        Driver.Create(
            Guid.NewGuid(), "Jane", "Doe",
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false),
            companyId ?? CompanyId);

    private static Truck SomeTruck(TruckSize size = TruckSize.Medium)
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, size);
        truck.AssignToCompany(CompanyId);
        return truck;
    }

    private static Mock<IUnitOfWork> SetUp(
        Driver? driver, IReadOnlyList<Truck>? trucks = null, Trip? openTrip = null, bool companyExists = true)
    {
        var drivers = new Mock<IDriverRepository>();
        drivers.Setup(d => d.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(driver);
        var truckRepo = new Mock<ITruckRepository>();
        truckRepo.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trucks ?? []);
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(openTrip);
        var companies = new Mock<ITruckingCompanyRepository>();
        companies.Setup(c => c.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) =>
                companyExists ? TruckingCompany.Create(id, "Some Company", GeoLocation.Create(53.55, 9.99), "Office place") : null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);
        unitOfWork.SetupGet(u => u.Trucks).Returns(truckRepo.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        unitOfWork.SetupGet(u => u.TruckingCompanies).Returns(companies.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task SetDriverCompanyAsync_DriverWithoutCompany_JoinsTheCompany()
    {
        var driver = SomeDriver();
        driver.UnassignFromCompany();
        var newCompanyId = Guid.NewGuid();
        var unitOfWork = SetUp(driver);

        var handler = new SetDriverCompanyHandler(unitOfWork.Object);
        await handler.SetDriverCompanyAsync(new SetDriverCompanyRequest(driver.Id, newCompanyId));

        Assert.Equal(newCompanyId, driver.TruckingCompanyId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetDriverCompanyAsync_DriverInAnotherCompany_Throws_NeverSaves()
    {
        var driver = SomeDriver();
        var unitOfWork = SetUp(driver);

        var handler = new SetDriverCompanyHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetDriverCompanyAsync(new SetDriverCompanyRequest(driver.Id, Guid.NewGuid())));
        Assert.Equal(CompanyId, driver.TruckingCompanyId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetDriverCompanyAsync_UnknownCompany_Throws_NeverSaves()
    {
        var driver = SomeDriver();
        driver.UnassignFromCompany();
        var unitOfWork = SetUp(driver, companyExists: false);

        var handler = new SetDriverCompanyHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetDriverCompanyAsync(new SetDriverCompanyRequest(driver.Id, Guid.NewGuid())));
        Assert.Null(driver.TruckingCompanyId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetDriverCompanyAsync_LeaveWhileNotOnATruck_ClearsCompany()
    {
        var driver = SomeDriver();
        var unitOfWork = SetUp(driver);

        var handler = new SetDriverCompanyHandler(unitOfWork.Object);
        await handler.SetDriverCompanyAsync(new SetDriverCompanyRequest(driver.Id, null));

        Assert.Null(driver.TruckingCompanyId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetDriverCompanyAsync_LeaveAsSingleDriver_TruckLosesDriverAndIsSwitchedOff()
    {
        var driver = SomeDriver();
        var truck = SomeTruck();
        truck.AssignDrivers(driver);
        truck.Activate();
        var unitOfWork = SetUp(driver, [truck]);

        var handler = new SetDriverCompanyHandler(unitOfWork.Object);
        await handler.SetDriverCompanyAsync(new SetDriverCompanyRequest(driver.Id, null));

        Assert.Null(driver.TruckingCompanyId);
        Assert.Null(truck.DriverAssignment);
        Assert.False(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetDriverCompanyAsync_LeaveFromTeam_OtherDriverStaysAloneAndTruckStaysActive()
    {
        var primary = SomeDriver();
        var leaving = SomeDriver();
        var truck = SomeTruck(TruckSize.Large);
        truck.AssignDrivers(primary, leaving);
        truck.Activate();
        var unitOfWork = SetUp(leaving, [truck]);

        var handler = new SetDriverCompanyHandler(unitOfWork.Object);
        await handler.SetDriverCompanyAsync(new SetDriverCompanyRequest(leaving.Id, null));

        Assert.Null(leaving.TruckingCompanyId);
        Assert.Equal(DriverConfigurationType.Single, truck.DriverAssignment!.ConfigurationType);
        Assert.Same(primary, truck.DriverAssignment.PrimaryDriver);
        Assert.True(truck.IsActive);
    }

    [Fact]
    public async Task SetDriverCompanyAsync_LeaveWhileTruckOnTrip_Throws_NothingChanges()
    {
        var driver = SomeDriver();
        var truck = SomeTruck();
        truck.AssignDrivers(driver);
        var openTrip = Trip.Open(truck.Id, CompanyId, new DateTime(2026, 1, 1, 6, 0, 0));
        var unitOfWork = SetUp(driver, [truck], openTrip);

        var handler = new SetDriverCompanyHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetDriverCompanyAsync(new SetDriverCompanyRequest(driver.Id, null)));
        Assert.Equal(CompanyId, driver.TruckingCompanyId);
        Assert.Same(driver, truck.DriverAssignment!.PrimaryDriver);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetDriverCompanyAsync_UnknownDriverId_Throws()
    {
        var unitOfWork = SetUp(null);

        var handler = new SetDriverCompanyHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetDriverCompanyAsync(new SetDriverCompanyRequest(Guid.NewGuid(), null)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
