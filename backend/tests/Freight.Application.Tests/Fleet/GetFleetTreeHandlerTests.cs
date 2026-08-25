using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetFleetTreeHandlerTests
{
    private static Driver NewDriver(string firstName) =>
        Driver.Create(
            firstName,
            "Doe",
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));

    [Fact]
    public async Task HandleAsync_ReturnsCompanyTrucksWithAssignmentsAndGloballyUnassignedDrivers()
    {
        var companyId = Guid.NewGuid();

        var assignedDriver = NewDriver("Assigned");
        var unassignedDriver = NewDriver("Unassigned");

        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Medium);
        truck.AssignToCompany(companyId);
        truck.AssignDrivers(assignedDriver);

        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByTruckingCompanyIdAsync(companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([truck]);

        var drivers = new Mock<IDriverRepository>();
        drivers.Setup(d => d.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([assignedDriver, unassignedDriver]);

        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);

        var handler = new GetFleetTreeHandler(unitOfWork.Object);

        var response = await handler.HandleAsync(new GetFleetTreeRequest(companyId));

        var truckDto = Assert.Single(response.Trucks);
        Assert.Equal(truck.Id, truckDto.TruckId);
        Assert.NotNull(truckDto.DriverAssignment);
        Assert.Equal(assignedDriver.Id, truckDto.DriverAssignment!.PrimaryDriver.DriverId);

        var unassignedDto = Assert.Single(response.UnassignedDrivers);
        Assert.Equal(unassignedDriver.Id, unassignedDto.DriverId);
    }
}
