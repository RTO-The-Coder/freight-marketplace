using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetTrucksHandlerTests
{
    private static (Mock<IUnitOfWork> UnitOfWork, Mock<ITruckRepository> Trucks) NewMocks()
    {
        var trucks = new Mock<ITruckRepository>();
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        return (unitOfWork, trucks);
    }

    [Fact]
    public async Task HandleAsync_UnassignedOnly_ReturnsOnlyTrucksWithoutCompany()
    {
        var (unitOfWork, trucks) = NewMocks();
        var unassignedTruck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        var assignedTruck = Truck.Create("Truck 2", TruckType.BoxVan, TruckSize.Small);
        assignedTruck.AssignToCompany(Guid.NewGuid());

        trucks.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([unassignedTruck, assignedTruck]);

        var handler = new GetTrucksHandler(unitOfWork.Object);

        var response = await handler.HandleAsync(new GetTrucksRequest(UnassignedOnly: true));

        var dto = Assert.Single(response.Trucks);
        Assert.Equal(unassignedTruck.Id, dto.TruckId);
    }

    [Fact]
    public async Task HandleAsync_NotUnassignedOnly_ReturnsAllTrucks()
    {
        var (unitOfWork, trucks) = NewMocks();
        var truck1 = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        var truck2 = Truck.Create("Truck 2", TruckType.BoxVan, TruckSize.Small);
        truck2.AssignToCompany(Guid.NewGuid());

        trucks.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([truck1, truck2]);

        var handler = new GetTrucksHandler(unitOfWork.Object);

        var response = await handler.HandleAsync(new GetTrucksRequest(UnassignedOnly: false));

        Assert.Equal(2, response.Trucks.Count);
    }

    [Fact]
    public async Task HandleAsync_TruckingCompanyIdFilter_ReturnsOnlyThatCompanysTrucksRegardlessOfUnassignedOnly()
    {
        var (unitOfWork, trucks) = NewMocks();
        var companyId = Guid.NewGuid();

        var matchingTruck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        matchingTruck.AssignToCompany(companyId);

        var otherCompanyTruck = Truck.Create("Truck 2", TruckType.BoxVan, TruckSize.Small);
        otherCompanyTruck.AssignToCompany(Guid.NewGuid());

        var unassignedTruck = Truck.Create("Truck 3", TruckType.BoxVan, TruckSize.Small);

        trucks.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([matchingTruck, otherCompanyTruck, unassignedTruck]);

        var handler = new GetTrucksHandler(unitOfWork.Object);

        var response = await handler.HandleAsync(new GetTrucksRequest(UnassignedOnly: true, TruckingCompanyId: companyId));

        var dto = Assert.Single(response.Trucks);
        Assert.Equal(matchingTruck.Id, dto.TruckId);
    }

    [Fact]
    public async Task HandleAsync_MapsHasDriverAssignmentFlag()
    {
        var (unitOfWork, trucks) = NewMocks();
        var driver = Driver.Create(
            "Jane",
            "Doe",
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));

        var truckWithDriver = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        truckWithDriver.AssignDrivers(driver);

        var truckWithoutDriver = Truck.Create("Truck 2", TruckType.BoxVan, TruckSize.Small);

        trucks.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([truckWithDriver, truckWithoutDriver]);

        var handler = new GetTrucksHandler(unitOfWork.Object);

        var response = await handler.HandleAsync(new GetTrucksRequest(UnassignedOnly: false));

        Assert.True(response.Trucks.Single(t => t.TruckId == truckWithDriver.Id).HasDriverAssignment);
        Assert.False(response.Trucks.Single(t => t.TruckId == truckWithoutDriver.Id).HasDriverAssignment);
    }
}
