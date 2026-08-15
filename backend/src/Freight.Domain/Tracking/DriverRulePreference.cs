namespace Freight.Domain.Tracking;

public sealed class DriverRulePreference
{
    public Guid DriverId { get; }
    public BreakPreference BreakPreference { get; }
    public DailyRestPreference DailyRestPreference { get; }
    public WeeklyRestPreference WeeklyRestPreference { get; }
    public bool ExtendDailyDrivingWhenEligible { get; }

    public DriverRulePreference(
        Guid driverId,
        BreakPreference breakPreference,
        DailyRestPreference dailyRestPreference,
        WeeklyRestPreference weeklyRestPreference,
        bool extendDailyDrivingWhenEligible)
    {
        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("Driver id cannot be empty.", nameof(driverId));
        }

        DriverId = driverId;
        BreakPreference = breakPreference;
        DailyRestPreference = dailyRestPreference;
        WeeklyRestPreference = weeklyRestPreference;
        ExtendDailyDrivingWhenEligible = extendDailyDrivingWhenEligible;
    }
}
