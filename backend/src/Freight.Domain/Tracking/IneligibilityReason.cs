namespace Freight.Domain.Tracking;

public enum IneligibilityReason
{
    OnBreak,
    OnDailyRest,
    OnWeeklyRest,
    DailyCapReached,
    WeeklyCapReached,
    TwoWeekCapReached
}
