namespace Freight.Domain.Fleet;

/// <summary>
/// A driver who drove on a <see cref="Trip"/>, with the name they had at the time - a
/// snapshot, so the trip's record stays readable after the driver is renamed, leaves the
/// company or is reassigned. Recorded by <see cref="Trip.RecordDriver"/>.
/// </summary>
public sealed class TripDriver
{
    public Guid DriverId { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    // EF Core materializer only.
    private TripDriver()
    {
    }

    internal TripDriver(Guid driverId, string firstName, string lastName)
    {
        DriverId = driverId;
        FirstName = firstName;
        LastName = lastName;
    }
}
