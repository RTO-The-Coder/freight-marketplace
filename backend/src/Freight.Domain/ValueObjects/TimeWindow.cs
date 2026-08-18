namespace Freight.Domain.ValueObjects;

public sealed record TimeWindow
{
    public DateTime Earliest { get; }
    public DateTime Latest { get; }

    private TimeWindow(DateTime earliest, DateTime latest)
    {
        Earliest = earliest;
        Latest = latest;
    }

    public static TimeWindow Create(DateTime earliest, DateTime latest)
    {
        if (earliest >= latest)
        {
            throw new ArgumentException("Earliest must be before latest.", nameof(earliest));
        }

        return new TimeWindow(earliest, latest);
    }
}
