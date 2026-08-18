namespace Freight.Domain.ValueObjects;

public sealed record Capacity
{
    public double WeightKg { get; }
    public double VolumeCubicMeters { get; }

    private Capacity(double weightKg, double volumeCubicMeters)
    {
        WeightKg = weightKg;
        VolumeCubicMeters = volumeCubicMeters;
    }

    public static Capacity Create(double weightKg, double volumeCubicMeters)
    {
        if (weightKg < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weightKg), weightKg, "Weight cannot be negative.");
        }

        if (volumeCubicMeters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(volumeCubicMeters), volumeCubicMeters, "Volume cannot be negative.");
        }

        return new Capacity(weightKg, volumeCubicMeters);
    }

    public bool CanAccommodate(Capacity required)
    {
        ArgumentNullException.ThrowIfNull(required);

        return WeightKg >= required.WeightKg && VolumeCubicMeters >= required.VolumeCubicMeters;
    }

    public Capacity Subtract(Capacity used)
    {
        ArgumentNullException.ThrowIfNull(used);

        if (!CanAccommodate(used))
        {
            throw new InvalidOperationException("Cannot subtract a capacity greater than what is available.");
        }

        return new Capacity(WeightKg - used.WeightKg, VolumeCubicMeters - used.VolumeCubicMeters);
    }
}
