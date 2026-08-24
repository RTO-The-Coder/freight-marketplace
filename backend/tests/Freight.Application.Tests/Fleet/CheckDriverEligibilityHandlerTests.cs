using Freight.Application.Fleet;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Tracking;
using Freight.Domain.Tracking.Abstractions;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Fleet;

public sealed class CheckDriverEligibilityHandlerTests
{
    private static Driver NewDriver() =>
        Driver.Create(
            "Jane",
            "Doe",
            DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false));

    private static (Mock<IUnitOfWork> UnitOfWork, Mock<IDriverRepository> Drivers, Mock<IDriverRuleEngine> RuleEngine) NewMocks()
    {
        var drivers = new Mock<IDriverRepository>();
        var ruleEngine = new Mock<IDriverRuleEngine>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);
        return (unitOfWork, drivers, ruleEngine);
    }

    [Fact]
    public async Task HandleAsync_DriverHasStartedDriving_ReturnsEngineResult()
    {
        var (unitOfWork, drivers, ruleEngine) = NewMocks();
        var driver = NewDriver();
        driver.StartDriving(new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc));

        drivers.Setup(d => d.GetByIdAsync(driver.Id, It.IsAny<CancellationToken>())).ReturnsAsync(driver);
        ruleEngine
            .Setup(e => e.IsEligibleToDriveFuture(driver.ComplianceState!, driver.Rules, 120, RestRuleLimits.Default))
            .Returns(new DriverEligibility(true, null, null));

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, ruleEngine.Object);

        var response = await handler.HandleAsync(new CheckDriverEligibilityRequest(driver.Id, 120));

        Assert.True(response.IsEligible);
        Assert.Null(response.Reason);
    }

    [Fact]
    public async Task HandleAsync_DriverNeverStartedDriving_Throws()
    {
        var (unitOfWork, drivers, ruleEngine) = NewMocks();
        var driver = NewDriver();

        drivers.Setup(d => d.GetByIdAsync(driver.Id, It.IsAny<CancellationToken>())).ReturnsAsync(driver);

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, ruleEngine.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new CheckDriverEligibilityRequest(driver.Id, 60)));
    }

    [Fact]
    public async Task HandleAsync_NegativeAfterMinutes_Throws()
    {
        var (unitOfWork, drivers, ruleEngine) = NewMocks();
        var driver = NewDriver();
        driver.StartDriving(DateTime.UtcNow);

        drivers.Setup(d => d.GetByIdAsync(driver.Id, It.IsAny<CancellationToken>())).ReturnsAsync(driver);

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, ruleEngine.Object);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            handler.HandleAsync(new CheckDriverEligibilityRequest(driver.Id, -1)));
    }

    [Fact]
    public async Task HandleAsync_UnknownDriverId_Throws()
    {
        var (unitOfWork, drivers, ruleEngine) = NewMocks();
        drivers.Setup(d => d.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Driver?)null);

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, ruleEngine.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new CheckDriverEligibilityRequest(Guid.NewGuid(), 60)));
    }
}
