namespace Freight.Domain.Tracking.Enums;

public enum IneligibilityReason
{
    OnBreak,
    OnDailyRest,
    OnWeeklyRest,
    DailyCapReached,
    WeeklyCapReached,
    TwoWeekCapReached,

    /// <summary>The 24h rule: the daily rest must start now to end within 24h of the last rest.</summary>
    DailyRestDue,

    /// <summary>The six-day rule: 144h since the last weekly rest ended.</summary>
    WeeklyRestDue
}
