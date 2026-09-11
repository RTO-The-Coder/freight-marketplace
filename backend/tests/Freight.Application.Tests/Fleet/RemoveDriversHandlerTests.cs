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
    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static Truck TruckWithDriver()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignDrivers(Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules()));
        return truck;
    }

    private static Mock<IUnitOfWork> SetUp(Truck? truck, Trip? openTrip)
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
    public async Task RemoveDriversAsync_NoOpenTrip_ClearsAssignmentDeactivatesAndSaves()
    {
        var truck = TruckWithDriver();
        var unitOfWork = SetUp(truck, null);

        var handler = new RemoveDriversHandler(unitOfWork.Object);
        await handler.RemoveDriversAsync(new RemoveDriversRequest(truck.Id));

        Assert.Null(truck.DriverAssignment);
        Assert.False(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveDriversAsync_OpenTripExists_ThrowsBeforeClearingAnything()
    {
        var truck = TruckWithDriver();
        var openTrip = Trip.Open(truck.Id, Guid.NewGuid(), new DateTime(2026, 1, 1, 6, 0, 0));
        var unitOfWork = SetUp(truck, openTrip);

        var handler = new RemoveDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.RemoveDriversAsync(new RemoveDriversRequest(truck.Id)));

        // The guard must run BEFORE any domain mutation - assignment untouched.
        Assert.NotNull(truck.DriverAssignment);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveDriversAsync_UnknownTruckId_Throws_NeverSaves()
    {
        var unitOfWork = SetUp(null, null);

        var handler = new RemoveDriversHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.RemoveDriversAsync(new RemoveDriversRequest(Guid.NewGuid())));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
