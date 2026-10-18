using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class SetTruckCompanyHandlerTests
{
    private static Truck SomeTruck() =>
        Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);

    private static Truck TruckWithDriverIn(Guid companyId)
    {
        var truck = SomeTruck();
        truck.AssignToCompany(companyId);
        truck.AssignDrivers(Driver.Create(
            Guid.NewGuid(), "Jane", "Doe",
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false),
            companyId));
        return truck;
    }

    private static Mock<IUnitOfWork> SetUp(Truck? truck, Trip? openTrip = null)
    {
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        var trips = new Mock<ITripRepository>();
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(openTrip);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task SetTruckCompanyAsync_CompanyIdSupplied_CallsAssignToCompany()
    {
        var truck = SomeTruck();
        var companyId = Guid.NewGuid();
        var unitOfWork = SetUp(truck);

        var handler = new SetTruckCompanyHandler(unitOfWork.Object);
        await handler.SetTruckCompanyAsync(new SetTruckCompanyRequest(truck.Id, companyId));

        Assert.Equal(companyId, truck.TruckingCompanyId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTruckCompanyAsync_TruckInAnotherCompany_Throws_NeverSaves()
    {
        var truck = SomeTruck();
        var companyId = Guid.NewGuid();
        truck.AssignToCompany(companyId);
        var unitOfWork = SetUp(truck);

        var handler = new SetTruckCompanyHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetTruckCompanyAsync(new SetTruckCompanyRequest(truck.Id, Guid.NewGuid())));
        Assert.Equal(companyId, truck.TruckingCompanyId);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetTruckCompanyAsync_NullCompanyId_LeavesCompanyAndTakesDriversOff()
    {
        var truck = TruckWithDriverIn(Guid.NewGuid());
        truck.Activate();
        var unitOfWork = SetUp(truck);

        var handler = new SetTruckCompanyHandler(unitOfWork.Object);
        await handler.SetTruckCompanyAsync(new SetTruckCompanyRequest(truck.Id, null));

        Assert.Null(truck.TruckingCompanyId);
        Assert.Null(truck.DriverAssignment);
        Assert.False(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTruckCompanyAsync_NullCompanyIdWhileOnTrip_Throws_NothingChanges()
    {
        var companyId = Guid.NewGuid();
        var truck = TruckWithDriverIn(companyId);
        var openTrip = Trip.Open(truck.Id, companyId, new DateTime(2026, 1, 1, 6, 0, 0));
        var unitOfWork = SetUp(truck, openTrip);

        var handler = new SetTruckCompanyHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetTruckCompanyAsync(new SetTruckCompanyRequest(truck.Id, null)));
        Assert.Equal(companyId, truck.TruckingCompanyId);
        Assert.NotNull(truck.DriverAssignment);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetTruckCompanyAsync_UnknownTruckId_Throws()
    {
        var unitOfWork = SetUp(null);

        var handler = new SetTruckCompanyHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetTruckCompanyAsync(new SetTruckCompanyRequest(Guid.NewGuid(), Guid.NewGuid())));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
