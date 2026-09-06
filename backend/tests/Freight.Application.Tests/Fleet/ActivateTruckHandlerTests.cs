using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Fleet.Enums;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class ActivateTruckHandlerTests
{
    private static Driver NewDriver() =>
        Driver.Create(Guid.NewGuid(), "Jane", "Doe",
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, extendDailyDrivingWhenEligible: false));

    [Fact]
    public async Task HandleAsync_TruckAssignedToCompanyAndDriver_ActivatesAndSaves()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(NewDriver());
        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);

        var handler = new SetTruckActivationHandler(unitOfWork.Object);

        await handler.SetTruckActivationAsync(new SetTruckActivationRequest(truck.Id, true));

        Assert.True(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_TruckWithoutCompany_ThrowsAndDoesNotSave()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);

        var handler = new SetTruckActivationHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetTruckActivationAsync(new SetTruckActivationRequest(truck.Id, true)));

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleActivation_Deactivate_DeactivatesAndSaves()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        var truck = Truck.Create("Truck 1", TruckType.BoxVan, TruckSize.Small);
        truck.AssignToCompany(Guid.NewGuid());
        truck.AssignDrivers(NewDriver());
        truck.Activate();
        trucks.Setup(t => t.GetByIdAsync(truck.Id, It.IsAny<CancellationToken>())).ReturnsAsync(truck);

        var handler = new SetTruckActivationHandler(unitOfWork.Object);

        await handler.SetTruckActivationAsync(new SetTruckActivationRequest(truck.Id, false));

        Assert.False(truck.IsActive);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleActivation_TruckNotFound_ThrowsAndDoesNotSave()
    {
        var trucks = new Mock<ITruckRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Trucks).Returns(trucks.Object);

        trucks.Setup(t => t.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Truck?)null);

        var handler = new SetTruckActivationHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.SetTruckActivationAsync(new SetTruckActivationRequest(Guid.NewGuid(), true)));

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
