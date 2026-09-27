using Freight.Domain.Common;
using Freight.Domain.Tracking.Enums;

namespace Freight.Domain.Tracking;

public sealed class DriverComplianceState : HasDomainEvents
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

    /// <summary>
    /// When the driver's last daily or weekly rest ended - the trip opening counts as one.
    /// Team driving uses it for the 30h rule: the next shared 9h rest must start within
    /// <see cref="ValueObjects.RestRuleLimits.TeamMaxMinutesBetweenDailyRests"/> of it.
    /// </summary>
    public DateTime LastRestEndedAt { get; internal set; }

    /// <summary>
    /// When the driver's last weekly rest ended - the trip opening counts as one. The next
    /// weekly rest must start within <see cref="ValueObjects.RestRuleLimits.MaxMinutesBetweenWeeklyRests"/>
    /// of it (the six-day rule).
    /// </summary>
    public DateTime LastWeeklyRestEndedAt { get; internal set; }

    /// <summary>
    /// Minutes a short (reduced) weekly rest still owes: 45h minus its length. Paid by
    /// adding them to the next weekly rest, which is then never reduced.
    /// </summary>
    public int WeeklyRestMinutesOwed { get; internal set; }

    /// <summary>
    /// Planned length of the running break/rest block (0 while driving or riding). Tells
    /// the split rest's 3h block apart from a full daily rest, and how long a daily rest
    /// has already run when the six-day rule turns it into the weekly rest.
    /// </summary>
    public int CurrentActivityLengthMinutes { get; internal set; }

    // EF Core cannot bind simulatedStart through the constructor below (it has no
    // corresponding property of the same name - it only seeds
    // LastEvaluatedSimulatedTime - and EF's constructor injection requires an exact
    // property match) - this parameterless constructor exists solely so EF's
    // materializer can construct an instance and set properties via reflection. The
    // public constructor below remains the only construction path reachable from
    // application code.
    private DriverComplianceState()
    {
    }

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
        LastRestEndedAt = simulatedStart;
        LastWeeklyRestEndedAt = simulatedStart;
    }

    /// <summary>
    /// A snapshot copy for hypothetical/what-if projection (e.g.
    /// <see cref="Abstractions.IDriverRuleEngine.IsEligibleToDriveFuture"/>) — never used
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
            WeeklyDrivingMinutesPriorWeek = WeeklyDrivingMinutesPriorWeek,
            LastRestEndedAt = LastRestEndedAt,
            LastWeeklyRestEndedAt = LastWeeklyRestEndedAt,
            WeeklyRestMinutesOwed = WeeklyRestMinutesOwed,
            CurrentActivityLengthMinutes = CurrentActivityLengthMinutes
        };
    }
}
