using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class SetTruckActivationHandlerTests
{
    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static Truck ActivatableTruck()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules()));
        return truck;
    }

    private static Mock<IUnitOfWork> SetUp(Truck? truck)
    {
        var trucks = new Mock<ITruckRepository>();
        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(truck);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task SetTruckActivationAsync_IsActiveTrue_CallsActivate()
    {
        var truck = ActivatableTruck();
        var unitOfWork = SetUp(truck);

        var handler = new SetTruckActivationHandler(unitOfWork.Object);
        await handler.SetTruckActivationAsync(new SetTruckActivationRequest(truck.Id, IsActive: true));

        Assert.True(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTruckActivationAsync_IsActiveFalse_CallsDeactivate()
    {
        var truck = ActivatableTruck();
        truck.Activate();
        var unitOfWork = SetUp(truck);

        var handler = new SetTruckActivationHandler(unitOfWork.Object);
        await handler.SetTruckActivationAsync(new SetTruckActivationRequest(truck.Id, IsActive: false));

        Assert.False(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetTruckActivationAsync_ActivateWithoutCompany_DomainRejectionPropagatesUnwrapped()
    {
        var truck = Truck.Create(Guid.NewGuid(), "Truck-1", TruckType.Refrigerated, TruckSize.Medium);
        truck.AssignDrivers(Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules()));
        var unitOfWork = SetUp(truck);

        var handler = new SetTruckActivationHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetTruckActivationAsync(new SetTruckActivationRequest(truck.Id, IsActive: true)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SetTruckActivationAsync_UnknownTruckId_Throws()
    {
        var unitOfWork = SetUp(null);

        var handler = new SetTruckActivationHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetTruckActivationAsync(new SetTruckActivationRequest(Guid.NewGuid(), IsActive: true)));
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
