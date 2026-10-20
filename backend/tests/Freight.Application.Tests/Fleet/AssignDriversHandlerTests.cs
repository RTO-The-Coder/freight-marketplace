using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class AssignDriversHandlerTests
{
    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static readonly Guid CompanyId = Guid.NewGuid();

    private static Driver SomeDriver(Guid? companyId = null) =>
        Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules(), companyId ?? CompanyId);

    private static Truck SomeTruck(TruckSize size)
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, size);
        truck.AssignToCompany(CompanyId);
        return truck;
    }

    private static Mock<IUnitOfWork> SetUp(Truck? truck, Dictionary<Guid, Driver> driversById, Trip? openTrip = null)
    {
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        var drivers = new Mock<IDriverRepository>();
        drivers.Setup(d => d.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => driversById.GetValueOrDefault(id));

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(openTrip);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task AssignDriversAsync_TruckOnAnOpenTrip_RecordsTheNewDriversOnTheTrip()
    {
        var truck = SomeTruck(TruckSize.Large);
        var first = SomeDriver();
        truck.AssignDrivers(first);
        var trip = Trip.Open(truck.Id, CompanyId, new DateTime(2026, 8, 1, 5, 0, 0, DateTimeKind.Utc));
        trip.RecordDriver(first);

        var primary = SomeDriver();
        var secondary = SomeDriver();
        var unitOfWork = SetUp(truck, new Dictionary<Guid, Driver> { [primary.Id] = primary, [secondary.Id] = secondary }, trip);

        await new AssignDriversHandler(unitOfWork.Object).AssignDriversAsync(new AssignDriversRequest(truck.Id, primary.Id, secondary.Id));

        Assert.Equal([first.Id, primary.Id, secondary.Id], trip.Drivers.Select(driver => driver.DriverId));
    }

    [Fact]
    public async Task AssignDriversAsync_PrimaryOnly_CallsAssignDriversWithNullSecondary()
    {
        var truck = SomeTruck(TruckSize.Medium);
        var primary = SomeDriver();
        var unitOfWork = SetUp(truck, new Dictionary<Guid, Driver> { [primary.Id] = primary });

        var handler = new AssignDriversHandler(unitOfWork.Object);
        await handler.AssignDriversAsync(new AssignDriversRequest(truck.Id, primary.Id, null));

        Assert.Equal(DriverConfigurationType.Single, truck.DriverAssignment!.ConfigurationType);
        Assert.Same(primary, truck.DriverAssignment.PrimaryDriver);
        Assert.Null(truck.DriverAssignment.SecondaryDriver);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignDriversAsync_PrimaryAndSecondary_CallsAssignDriversWithBoth()
    {
        var truck = SomeTruck(TruckSize.Large);
        var primary = SomeDriver();
        var secondary = SomeDriver();
        var unitOfWork = SetUp(truck, new Dictionary<Guid, Driver> { [primary.Id] = primary, [secondary.Id] = secondary });

        var handler = new AssignDriversHandler(unitOfWork.Object);
        await handler.AssignDriversAsync(new AssignDriversRequest(truck.Id, primary.Id, secondary.Id));

        Assert.Equal(DriverConfigurationType.Team, truck.DriverAssignment!.ConfigurationType);
        Assert.Same(secondary, truck.DriverAssignment.SecondaryDriver);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AssignDriversAsync_DriverOfAnotherCompany_Throws_NeverSaves()
    {
        var truck = SomeTruck(TruckSize.Medium);
        var otherCompanysDriver = SomeDriver(Guid.NewGuid());
        var unitOfWork = SetUp(truck, new Dictionary<Guid, Driver> { [otherCompanysDriver.Id] = otherCompanysDriver });

        var handler = new AssignDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignDriversAsync(new AssignDriversRequest(truck.Id, otherCompanysDriver.Id, null)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(truck.DriverAssignment);
    }

    [Fact]
    public async Task AssignDriversAsync_TruckWithoutCompany_Throws_NeverSaves()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        var driver = SomeDriver();
        var unitOfWork = SetUp(truck, new Dictionary<Guid, Driver> { [driver.Id] = driver });

        var handler = new AssignDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignDriversAsync(new AssignDriversRequest(truck.Id, driver.Id, null)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(truck.DriverAssignment);
    }

    [Fact]
    public async Task AssignDriversAsync_UnknownTruckId_Throws_NeverSaves()
    {
        var unitOfWork = SetUp(null, []);

        var handler = new AssignDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignDriversAsync(new AssignDriversRequest(Guid.NewGuid(), Guid.NewGuid(), null)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignDriversAsync_UnknownPrimaryDriverId_Throws_NeverSaves()
    {
        var truck = SomeTruck(TruckSize.Medium);
        var unitOfWork = SetUp(truck, []);

        var handler = new AssignDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignDriversAsync(new AssignDriversRequest(truck.Id, Guid.NewGuid(), null)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(truck.DriverAssignment);
    }

    [Fact]
    public async Task AssignDriversAsync_UnknownSecondaryDriverId_Throws_NeverSaves()
    {
        var truck = SomeTruck(TruckSize.Large);
        var primary = SomeDriver();
        var unitOfWork = SetUp(truck, new Dictionary<Guid, Driver> { [primary.Id] = primary });

        var handler = new AssignDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.AssignDriversAsync(new AssignDriversRequest(truck.Id, primary.Id, Guid.NewGuid())));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(truck.DriverAssignment);
    }
}
