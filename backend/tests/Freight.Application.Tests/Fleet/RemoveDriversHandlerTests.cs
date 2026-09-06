using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class RemoveDriversHandlerTests
{
    private static Driver NewDriver() =>
        Driver.Create("Jane", "Doe",
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));

    private static (Mock<IUnitOfWork> UnitOfWork, Mock<ITruckRepository> Trucks, Mock<ITripRepository> Trips) NewMocks()
    {
        var trucks = new Mock<ITruckRepository>();
        var trips = new Mock<ITripRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        unitOfWork.SetupGet(u => u.Trips).Returns(trips.Object);
        return (unitOfWork, trucks, trips);
    }

    private static Truck ActiveTruckWithDriver()
    {
        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Medium);
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(NewDriver());
        truck.Activate();
        return truck;
    }

    [Fact]
    public async Task RemoveDrivers_NoOpenTrip_ClearsAssignmentDeactivatesAndSaves()
    {
        var (unitOfWork, trucks, trips) = NewMocks();
        var truck = ActiveTruckWithDriver();

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync((Trip?)null);

        var handler = new RemoveDriversHandler(unitOfWork.Object);

        await handler.RemoveDriversAsync(new RemoveDriversRequest(truck.Id));

        Assert.Null(truck.DriverAssignment);
        Assert.False(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveDrivers_TruckHasOpenTrip_ThrowsAndDoesNotSave()
    {
        var (unitOfWork, trucks, trips) = NewMocks();
        var truck = ActiveTruckWithDriver();
        var openTrip = Trip.Open(truck.Id, Guid.NewGuid(), DateTime.UtcNow);

        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        trips.Setup(t => t.GetOpenTripByTruckIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(openTrip);

        var handler = new RemoveDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.RemoveDriversAsync(new RemoveDriversRequest(truck.Id)));

        Assert.NotNull(truck.DriverAssignment);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveDrivers_UnknownTruckId_ThrowsAndDoesNotSave()
    {
        var (unitOfWork, trucks, _) = NewMocks();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Truck?)null);

        var handler = new RemoveDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.RemoveDriversAsync(new RemoveDriversRequest(Guid.NewGuid())));

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
