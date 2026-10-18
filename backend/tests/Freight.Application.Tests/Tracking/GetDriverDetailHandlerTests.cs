using Freight.Application.Tracking;
using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.Fleet.Abstractions;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.ValueObjects;
using Freight.Domain.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;
using Moq;

namespace Freight.Application.Tests.Tracking;

public sealed class GetDriverDetailHandlerTests
{
    private static DrivingRules SomeRules() =>
        DrivingRules.Create(DrivingBreakRule.SplitBreak, DailyRestRule.ReducedRest, WeeklyRestRule.ReducedWeeklyRest, true);

    private static Mock<IUnitOfWork> SetUp(Driver? driver)
    {
        var drivers = new Mock<IDriverRepository>();
        drivers.Setup(d => d.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(driver);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.Drivers).Returns(drivers.Object);
        return unitOfWork;
    }

    [Fact]
    public async Task GetDriverDetailAsync_NeverDispatched_ComplianceStateIsNullNotThrow()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules(), Guid.NewGuid());
        var unitOfWork = SetUp(driver);

        var handler = new GetDriverDetailHandler(unitOfWork.Object);
        var dto = await handler.GetDriverDetailAsync(new GetDriverDetailRequest(driver.Id));

        Assert.Null(dto.ComplianceState);
        Assert.Equal(driver.Id, dto.DriverId);
        Assert.Equal(DrivingBreakRule.SplitBreak, dto.BreakRule);
        Assert.Equal(DailyRestRule.ReducedRest, dto.DailyRestRule);
        Assert.Equal(WeeklyRestRule.ReducedWeeklyRest, dto.WeeklyRestRule);
        Assert.True(dto.ExtendDailyDrivingWhenEligible);
    }

    [Fact]
    public async Task GetDriverDetailAsync_HasComplianceLedger_MapsEveryField()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules(), Guid.NewGuid());
        var tripStart = new DateTime(2026, 1, 1, 6, 0, 0);
        driver.ResetComplianceForNewTrip(tripStart);
        var unitOfWork = SetUp(driver);

        var handler = new GetDriverDetailHandler(unitOfWork.Object);
        var dto = await handler.GetDriverDetailAsync(new GetDriverDetailRequest(driver.Id));

        Assert.NotNull(dto.ComplianceState);
        var ledger = driver.ComplianceState!;
        Assert.Equal(ledger.CurrentActivity, dto.ComplianceState!.CurrentActivity);
        Assert.Equal(ledger.MinutesRemainingInCurrentActivity, dto.ComplianceState.MinutesRemainingInCurrentActivity);
        Assert.Equal(ledger.ContinuousDrivingMinutesSinceBreak, dto.ComplianceState.ContinuousDrivingMinutesSinceBreak);
        Assert.Equal(ledger.DailyDrivingMinutesToday, dto.ComplianceState.DailyDrivingMinutesToday);
        Assert.Equal(ledger.IsTodayExtended, dto.ComplianceState.IsTodayExtended);
        Assert.Equal(ledger.WeeklyDrivingMinutesThisWeek, dto.ComplianceState.WeeklyDrivingMinutesThisWeek);
        Assert.Equal(ledger.WeeklyDrivingMinutesPriorWeek, dto.ComplianceState.WeeklyDrivingMinutesPriorWeek);
        Assert.Equal(tripStart, dto.ComplianceState.LastEvaluatedSimulatedTime);
        Assert.Equal(ledger.AwaitingSecondBreakBlock, dto.ComplianceState.AwaitingSecondBreakBlock);
        Assert.Equal(ledger.ExtendedDaysUsedThisWeek, dto.ComplianceState.ExtendedDaysUsedThisWeek);
        Assert.Equal(ledger.AwaitingSecondDailyRestBlock, dto.ComplianceState.AwaitingSecondDailyRestBlock);
        Assert.Equal(ledger.ReducedDailyRestsUsedSinceWeeklyRest, dto.ComplianceState.ReducedDailyRestsUsedSinceWeeklyRest);
        Assert.Equal(tripStart, dto.ComplianceState.LastRestEndedAt);
        Assert.Equal(tripStart, dto.ComplianceState.LastWeeklyRestEndedAt);
        Assert.Equal(ledger.WeeklyRestMinutesOwed, dto.ComplianceState.WeeklyRestMinutesOwed);
        Assert.Equal(ledger.CurrentActivityLengthMinutes, dto.ComplianceState.CurrentActivityLengthMinutes);
    }

    [Fact]
    public async Task GetDriverDetailAsync_ReturnsTheEuLimitsTheLedgerIsMeasuredAgainst()
    {
        var driver = Driver.Create(Guid.NewGuid(), "Jane", "Doe", SomeRules(), Guid.NewGuid());
        var unitOfWork = SetUp(driver);

        var handler = new GetDriverDetailHandler(unitOfWork.Object);
        var dto = await handler.GetDriverDetailAsync(new GetDriverDetailRequest(driver.Id));

        var limits = RestRuleLimits.Default;
        Assert.Equal(limits.MaxContinuousDrivingMinutesBeforeBreak, dto.Limits.MaxContinuousDrivingMinutesBeforeBreak);
        Assert.Equal(limits.MaxDailyDrivingMinutes, dto.Limits.MaxDailyDrivingMinutes);
        Assert.Equal(limits.ExtendedDailyDrivingMinutes, dto.Limits.ExtendedDailyDrivingMinutes);
        Assert.Equal(limits.MaxExtendedDaysPerWeek, dto.Limits.MaxExtendedDaysPerWeek);
        Assert.Equal(limits.MaxReducedDailyRestsSinceWeeklyRest, dto.Limits.MaxReducedDailyRestsSinceWeeklyRest);
        Assert.Equal(limits.MaxWeeklyDrivingMinutes, dto.Limits.MaxWeeklyDrivingMinutes);
        Assert.Equal(limits.MaxTwoWeekDrivingMinutes, dto.Limits.MaxTwoWeekDrivingMinutes);
        Assert.Equal(limits.MaxMinutesBetweenWeeklyRests, dto.Limits.MaxMinutesBetweenWeeklyRests);
    }

    [Fact]
    public async Task GetDriverDetailAsync_UnknownDriverId_Throws()
    {
        var unitOfWork = SetUp(null);

        var handler = new GetDriverDetailHandler(unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.GetDriverDetailAsync(new GetDriverDetailRequest(Guid.NewGuid())));
    }
}
