using Freight.Domain.Common;

namespace Freight.Domain.Tracking;

public sealed class DriverComplianceState : Entity
{
    public Guid DriverId { get; }
    public DriverActivity CurrentActivity { get; internal set; }
    public int MinutesRemainingInCurrentActivity { get; internal set; }

    public int ContinuousDrivingMinutesSinceBreak { get; internal set; }
    public bool AwaitingSecondBreakBlock { get; internal set; }

    public int DailyDrivingMinutesToday { get; internal set; }
    public int ExtendedDaysUsedThisWeek { get; internal set; }
    public bool IsTodayExtended { get; internal set; }

    public bool AwaitingSecondDailyRestBlock { get; internal set; }
    public int ReducedDailyRestsUsedSinceWeeklyRest { get; internal set; }

    public int WeeklyDrivingMinutesThisWeek { get; internal set; }
    public int WeeklyDrivingMinutesPriorWeek { get; internal set; }

    public DateTime LastEvaluatedSimulatedTime { get; internal set; }

    public DriverComplianceState(Guid driverId, DateTime simulatedStart)
    {
        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver id cannot be empty.", nameof(driverId));
        }

        DriverId = driverId;
        CurrentActivity = DriverActivity.Driving;
        MinutesRemainingInCurrentActivity = 0;
        LastEvaluatedSimulatedTime = simulatedStart;
    }

    /// <summary>
    /// A snapshot copy for hypothetical/what-if projection (e.g.
    /// <see cref="Abstractions.IRestRuleEngine.IsEligibleToDriveFuture"/>) — never used
    /// to mutate the real, tracked ledger. All fields are value types, so this is a
    /// complete copy, not just a reference-shallow one.
    /// </summary>
    internal DriverComplianceState Clone()
    {
        return new DriverComplianceState(DriverId, LastEvaluatedSimulatedTime)
        {
            CurrentActivity = CurrentActivity,
            MinutesRemainingInCurrentActivity = MinutesRemainingInCurrentActivity,
            ContinuousDrivingMinutesSinceBreak = ContinuousDrivingMinutesSinceBreak,
            AwaitingSecondBreakBlock = AwaitingSecondBreakBlock,
            DailyDrivingMinutesToday = DailyDrivingMinutesToday,
            ExtendedDaysUsedThisWeek = ExtendedDaysUsedThisWeek,
            IsTodayExtended = IsTodayExtended,
            AwaitingSecondDailyRestBlock = AwaitingSecondDailyRestBlock,
            ReducedDailyRestsUsedSinceWeeklyRest = ReducedDailyRestsUsedSinceWeeklyRest,
            WeeklyDrivingMinutesThisWeek = WeeklyDrivingMinutesThisWeek,
            WeeklyDrivingMinutesPriorWeek = WeeklyDrivingMinutesPriorWeek
        };
    }
}
