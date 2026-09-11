using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class GetTrucksHandlerTests
{
    private static Truck SomeTruck() =>
        Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);

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
    public async Task GetTrucksAsync_NoFilters_ReturnsAllTrucks()
    {
        var truckA = SomeTruck();
        var truckB = SomeTruck();
        var unitOfWork = SetUp([truckA, truckB]);

        var handler = new GetTrucksHandler(unitOfWork.Object);
        var response = await handler.GetTrucksAsync(new GetTrucksRequest(UnassignedOnly: false));

        Assert.Equal(2, response.Trucks.Count);
    }

    [Fact]
    public async Task GetTrucksAsync_TruckingCompanyIdSet_FiltersByCompanyRegardlessOfUnassignedOnly()
    {
        var companyId = Guid.NewGuid();
        var truckInCompany = SomeTruck();
        truckInCompany.AssignToCompany(companyId);
        var truckUnassigned = SomeTruck();
        var unitOfWork = SetUp([truckInCompany, truckUnassigned]);

        var handler = new GetTrucksHandler(unitOfWork.Object);
        // UnassignedOnly is ALSO true here - TruckingCompanyId must still win (the else-if
        // precedence): only the in-company truck should come back, not the unassigned one.
        var response = await handler.GetTrucksAsync(new GetTrucksRequest(UnassignedOnly: true, TruckingCompanyId: companyId));

        var dto = Assert.Single(response.Trucks);
        Assert.Equal(truckInCompany.Id, dto.TruckId);
    }

    [Fact]
    public async Task GetTrucksAsync_UnassignedOnlyTrueNoCompanyId_FiltersToCompanylessTrucks()
    {
        var truckInCompany = SomeTruck();
        truckInCompany.AssignToCompany(Guid.NewGuid());
        var truckUnassigned = SomeTruck();
        var unitOfWork = SetUp([truckInCompany, truckUnassigned]);

        var handler = new GetTrucksHandler(unitOfWork.Object);
        var response = await handler.GetTrucksAsync(new GetTrucksRequest(UnassignedOnly: true));

        var dto = Assert.Single(response.Trucks);
        Assert.Equal(truckUnassigned.Id, dto.TruckId);
    }

    [Fact]
    public async Task GetTrucksAsync_MapsEveryDtoFieldIncludingHasDriverAssignment()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe",
            Freight.Domain.ValueObjects.DrivingRules.Create(
                Freight.Domain.ValueObjects.RuleVariants.DrivingBreakRule.FullBreak,
                Freight.Domain.ValueObjects.RuleVariants.DailyRestRule.FullRest,
                Freight.Domain.ValueObjects.RuleVariants.WeeklyRestRule.FullWeeklyRest, false));
        var truck = SomeTruck();
        truck.AssignDrivers(driver);
        var unitOfWork = SetUp([truck]);

        var handler = new GetTrucksHandler(unitOfWork.Object);
        var response = await handler.GetTrucksAsync(new GetTrucksRequest(UnassignedOnly: false));

        var dto = Assert.Single(response.Trucks);
        Assert.Equal(truck.Id, dto.TruckId);
        Assert.Equal(truck.TruckName, dto.TruckName);
        Assert.Equal(truck.Type, dto.TruckType);
        Assert.Equal(truck.Size, dto.TruckSize);
        Assert.Equal(truck.IsActive, dto.IsActive);
        Assert.Equal(truck.TruckingCompanyId, dto.TruckingCompanyId);
        Assert.True(dto.HasDriverAssignment);
    }

    [Fact]
    public async Task GetTrucksAsync_NoDriverAssignment_HasDriverAssignmentIsFalse()
    {
        var truck = SomeTruck();
        var unitOfWork = SetUp([truck]);

        var handler = new GetTrucksHandler(unitOfWork.Object);
        var response = await handler.GetTrucksAsync(new GetTrucksRequest(UnassignedOnly: false));

        Assert.False(Assert.Single(response.Trucks).HasDriverAssignment);
    }

    [Fact]
    public async Task GetTrucksAsync_LooksUpOpenTripPerTruckToDetermineStatus()
    {
        var truck = SomeTruck();
        var truckRepo = new Mock<ITruckRepository>();
        truckRepo.Setup(t => t.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([truck]);
        var tripRepo = new Mock<ITripRepository>();
        tripRepo.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(truckRepo.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(tripRepo.Object);

        var handler = new GetTrucksHandler(unitOfWork.Object);
        await handler.GetTrucksAsync(new GetTrucksRequest(UnassignedOnly: false));

        tripRepo.Verify(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
