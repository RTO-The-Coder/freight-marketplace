using Freight.Domain.Common;
using Freight.Domain.Tracking.Enums;
using Freight.Domain.Tracking.ValueObjects;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Application.Tracking;

public sealed record GetDriverDetailRequest(Guid DriverId);

/// <summary>The driver's whole compliance ledger, as the simulation tracks it.</summary>
public sealed record DriverComplianceStateDto(
    DriverActivity CurrentActivity,
    int MinutesRemainingInCurrentActivity,
    int ContinuousDrivingMinutesSinceBreak,
    int DailyDrivingMinutesToday,
    bool IsTodayExtended,
    int WeeklyDrivingMinutesThisWeek,
    int WeeklyDrivingMinutesPriorWeek,
    DateTime LastEvaluatedSimulatedTime,
    bool AwaitingSecondBreakBlock,
    int ExtendedDaysUsedThisWeek,
    bool AwaitingSecondDailyRestBlock,
    int ReducedDailyRestsUsedSinceWeeklyRest,
    DateTime LastRestEndedAt,
    DateTime LastWeeklyRestEndedAt,
    int WeeklyRestMinutesOwed,
    int CurrentActivityLengthMinutes);

/// <summary>
/// The EU 561 limits the ledger is measured against (<see cref="RestRuleLimits.Default"/>), so
/// clients can show "3 h 10 of 4 h 30" without hard-coding the numbers.
/// </summary>
public sealed record DriverRuleLimitsDto(
    int MaxContinuousDrivingMinutesBeforeBreak,
    int MaxDailyDrivingMinutes,
    int ExtendedDailyDrivingMinutes,
    int MaxExtendedDaysPerWeek,
    int MaxReducedDailyRestsSinceWeeklyRest,
    int MaxWeeklyDrivingMinutes,
    int MaxTwoWeekDrivingMinutes,
    int MaxMinutesBetweenWeeklyRests);

public sealed record DriverDetailDto(
    Guid DriverId,
    string FirstName,
    string LastName,
    DrivingBreakRule BreakRule,
    DailyRestRule DailyRestRule,
    WeeklyRestRule WeeklyRestRule,
    bool ExtendDailyDrivingWhenEligible,
    DriverComplianceStateDto? ComplianceState,
    Guid? TruckingCompanyId,
    DriverRuleLimitsDto Limits);

public sealed class GetDriverDetailHandler(IUnitOfWork unitOfWork)
{
    public async Task<DriverDetailDto> GetDriverDetailAsync(GetDriverDetailRequest request, CancellationToken cancellationToken = default)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new InvalidOperationException($"Driver '{request.DriverId}' was not found.");

        var limits = RestRuleLimits.Default;
        var state = driver.ComplianceState;

        return new DriverDetailDto(
            driver.Id,
            driver.FirstName,
            driver.LastName,
            driver.Rules.BreakRule,
            driver.Rules.DailyRestRule,
            driver.Rules.WeeklyRestRule,
            driver.Rules.ExtendDailyDrivingWhenEligible,
            state is null
                ? null
                : new DriverComplianceStateDto(
                    state.CurrentActivity,
                    state.MinutesRemainingInCurrentActivity,
                    state.ContinuousDrivingMinutesSinceBreak,
                    state.DailyDrivingMinutesToday,
                    state.IsTodayExtended,
                    state.WeeklyDrivingMinutesThisWeek,
                    state.WeeklyDrivingMinutesPriorWeek,
                    state.LastEvaluatedSimulatedTime,
                    state.AwaitingSecondBreakBlock,
                    state.ExtendedDaysUsedThisWeek,
                    state.AwaitingSecondDailyRestBlock,
                    state.ReducedDailyRestsUsedSinceWeeklyRest,
                    state.LastRestEndedAt,
                    state.LastWeeklyRestEndedAt,
                    state.WeeklyRestMinutesOwed,
                    state.CurrentActivityLengthMinutes),
            driver.TruckingCompanyId,
            new DriverRuleLimitsDto(
                limits.MaxContinuousDrivingMinutesBeforeBreak,
                limits.MaxDailyDrivingMinutes,
                limits.ExtendedDailyDrivingMinutes,
                limits.MaxExtendedDaysPerWeek,
                limits.MaxReducedDailyRestsSinceWeeklyRest,
                limits.MaxWeeklyDrivingMinutes,
                limits.MaxTwoWeekDrivingMinutes,
                limits.MaxMinutesBetweenWeeklyRests));
    }
}
