using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetTruckForDriverHandlerTests
{
    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static Driver SomeDriver() => Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());

    private static Mock<IUnitOfWork> SetUp(IReadOnlyList<Truck> trucks)
    {
        var truckRepo = new Mock<ITruckRepository>();
        truckRepo.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(trucks);
        var tripRepo = new Mock<ITripRepository>();
        tripRepo.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(truckRepo.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(tripRepo.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task GetTruckForDriverAsync_DriverIsPrimary_FindsTruck()
    {
        var driver = SomeDriver();
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignDrivers(driver);
        var unitOfWork = SetUp([truck]);

        var handler = new GetTruckForDriverHandler(unitOfWork.Object);
        var response = await handler.GetTruckForDriverAsync(new GetTruckForDriverRequest(driver.Id));

        Assert.NotNull(response.Truck);
        Assert.Equal(truck.Id, response.Truck!.TruckId);
        Assert.True(response.Truck.HasDriverAssignment);
    }

    [Fact]
    public async Task GetTruckForDriverAsync_DriverIsSecondary_FindsTruck()
    {
        var primary = SomeDriver();
        var secondary = SomeDriver();
        var otherTruck = Truck.Create(Guid.NewGuid(), "Truck-Other", TruckType.Refrigerated, TruckSize.Medium);
        otherTruck.AssignDrivers(SomeDriver());
        var teamTruck = Truck.Create(Guid.NewGuid(), "Truck-Team", TruckType.Refrigerated, TruckSize.Large);
        teamTruck.AssignDrivers(primary, secondary);
        var unitOfWork = SetUp([otherTruck, teamTruck]);

        var handler = new GetTruckForDriverHandler(unitOfWork.Object);
        var response = await handler.GetTruckForDriverAsync(new GetTruckForDriverRequest(secondary.Id));

        Assert.NotNull(response.Truck);
        Assert.Equal(teamTruck.Id, response.Truck!.TruckId);
    }

    [Fact]
    public async Task GetTruckForDriverAsync_DriverHasNoTruck_ReturnsNullTruckNotThrow()
    {
        var unitOfWork = SetUp([]);

        var handler = new GetTruckForDriverHandler(unitOfWork.Object);
        var response = await handler.GetTruckForDriverAsync(new GetTruckForDriverRequest(Guid.NewGuid()));

        Assert.Null(response.Truck);
    }
}
