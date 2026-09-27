namespace Freight.Domain.Tracking.Enums;

public enum DriverActivity
{
    Driving,
    OnBreak,
    OnDailyRest,
    OnWeeklyRest,

    /// <summary>
    /// Team co-driver riding while the other drives. Counts towards the break only
    /// (MinutesRemainingInCurrentActivity = break minutes still needed), never as rest.
    /// </summary>
    Passenger
}
