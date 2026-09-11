using Freight.Application.Tracking;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Tracking.Services;
using Freight.Domain.Tracking.ValueObjects;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Tracking;

public sealed class CheckDriverEligibilityHandlerTests
{
    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.FullBreak, DailyRestRule.FullRest, WeeklyRestRule.FullWeeklyRest, false);

    private static Mock<IUnitOfWork> SetUp(Driver? driver)
    {
        var drivers = new Mock<IDriverRepository>();
        drivers.Setup(d => d.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(driver);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task CheckDriverEligibilityAsync_FreshlyDispatchedDriver_IsEligibleWithinBreakBoundary()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());
        driver.ResetComplianceForNewTrip(new DateTime(2026, 1, 1, 6, 0, 0));
        var unitOfWork = SetUp(driver);

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, new DriverRuleEngine());
        var response = await handler.CheckDriverEligibilityAsync(new CheckDriverEligibilityRequest(driver.Id, AfterMinutes: 60));

        Assert.True(response.IsEligible);
        Assert.Null(response.Reason);
    }

    [Fact]
    public async Task CheckDriverEligibilityAsync_PastBreakBoundary_ReturnsIneligibleWithReason()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());
        driver.ResetComplianceForNewTrip(new DateTime(2026, 1, 1, 6, 0, 0));
        var unitOfWork = SetUp(driver);

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, new DriverRuleEngine());
        var afterMinutes = RestRuleLimits.Default.MaxContinuousDrivingMinutesBeforeBreak + 1;
        var response = await handler.CheckDriverEligibilityAsync(new CheckDriverEligibilityRequest(driver.Id, afterMinutes));

        Assert.False(response.IsEligible);
        Assert.NotNull(response.Reason);
    }

    [Fact]
    public async Task CheckDriverEligibilityAsync_IsGenuinelyReadOnly_NeverMutatesRealLedger()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());
        var tripStart = new DateTime(2026, 1, 1, 6, 0, 0);
        driver.ResetComplianceForNewTrip(tripStart);
        var unitOfWork = SetUp(driver);
        var originalDaily = driver.ComplianceState!.DailyDrivingMinutesToday;
        var originalLastEvaluated = driver.ComplianceState.LastEvaluatedSimulatedTime;

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, new DriverRuleEngine());
        await handler.CheckDriverEligibilityAsync(new CheckDriverEligibilityRequest(driver.Id, AfterMinutes: 500));

        Assert.Equal(originalDaily, driver.ComplianceState.DailyDrivingMinutesToday);
        Assert.Equal(originalLastEvaluated, driver.ComplianceState.LastEvaluatedSimulatedTime);
    }

    [Fact]
    public async Task CheckDriverEligibilityAsync_NegativeAfterMinutes_ThrowsBeforeAnyRepoCall()
    {
        var drivers = new Mock<IDriverRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, new DriverRuleEngine());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            handler.CheckDriverEligibilityAsync(new CheckDriverEligibilityRequest(Guid.NewGuid(), AfterMinutes: -1)));
        drivers.Verify(d => d.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckDriverEligibilityAsync_UnknownDriverId_Throws()
    {
        var unitOfWork = SetUp(null);

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, new DriverRuleEngine());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.CheckDriverEligibilityAsync(new CheckDriverEligibilityRequest(Guid.NewGuid(), AfterMinutes: 10)));
    }

    [Fact]
    public async Task CheckDriverEligibilityAsync_DriverNeverStartedDriving_ThrowsDistinctMessageFromNotFound()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules());
        var unitOfWork = SetUp(driver);

        var handler = new CheckDriverEligibilityHandler(unitOfWork.Object, new DriverRuleEngine());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.CheckDriverEligibilityAsync(new CheckDriverEligibilityRequest(driver.Id, AfterMinutes: 10)));
        Assert.Contains("never started driving", exception.Message);
    }
}
