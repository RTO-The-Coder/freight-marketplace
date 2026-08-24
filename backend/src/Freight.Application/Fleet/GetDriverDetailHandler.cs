using Freight.Domain.Common;
using Freight.Domain.Tracking;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Application.Fleet;

public sealed record GetDriverDetailRequest(Guid DriverId);

public sealed record DriverComplianceStateDto(
    DriverActivity CurrentActivity,
    int MinutesRemainingInCurrentActivity,
    int ContinuousDrivingMinutesSinceBreak,
    int DailyDrivingMinutesToday,
    bool IsTodayExtended,
    int WeeklyDrivingMinutesThisWeek,
    int WeeklyDrivingMinutesPriorWeek,
    DateTime LastEvaluatedSimulatedTime);

public sealed record DriverDetailDto(
    Guid DriverId,
    string FirstName,
    string LastName,
    DrivingBreakRule BreakRule,
    DailyRestRule DailyRestRule,
    WeeklyRestRule WeeklyRestRule,
    bool ExtendDailyDrivingWhenEligible,
    DriverComplianceStateDto? ComplianceState);

public sealed class GetDriverDetailHandler(IUnitOfWork unitOfWork)
{
    public async Task<DriverDetailDto> HandleAsync(GetDriverDetailRequest request, CancellationToken cancellationToken = default)
    {
        var driver = await unitOfWork.Drivers.GetByIdAsync(request.DriverId, cancellationToken)
            ?? throw new InvalidOperationException($"Driver '{request.DriverId}' was not found.");

        return new DriverDetailDto(
            driver.Id,
            driver.FirstName,
            driver.LastName,
            driver.Rules.BreakRule,
            driver.Rules.DailyRestRule,
            driver.Rules.WeeklyRestRule,
            driver.Rules.ExtendDailyDrivingWhenEligible,
            driver.ComplianceState is null
                ? null
                : new DriverComplianceStateDto(
                    driver.ComplianceState.CurrentActivity,
                    driver.ComplianceState.MinutesRemainingInCurrentActivity,
                    driver.ComplianceState.ContinuousDrivingMinutesSinceBreak,
                    driver.ComplianceState.DailyDrivingMinutesToday,
                    driver.ComplianceState.IsTodayExtended,
                    driver.ComplianceState.WeeklyDrivingMinutesThisWeek,
                    driver.ComplianceState.WeeklyDrivingMinutesPriorWeek,
                    driver.ComplianceState.LastEvaluatedSimulatedTime));
    }
}
