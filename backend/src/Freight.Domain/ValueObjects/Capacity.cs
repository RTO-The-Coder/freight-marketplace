using Freight.Domain.Fleet;

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

    /// <summary>
    /// The fixed capacity for a <see cref="TruckSize"/> tier. A truck's capacity is
    /// always derived from its size - never entered independently - so this lookup is
    /// the single source of truth for the three tiers.
    /// </summary>
    public static Capacity ForTruckSize(TruckSize size) => size switch
    {
        TruckSize.Small => Create(2_800, 20),
        TruckSize.Medium => Create(9_000, 45),
        TruckSize.Large => Create(24_000, 90),
        _ => throw new ArgumentOutOfRangeException(nameof(size), size, "Unknown truck size."),
    };

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
