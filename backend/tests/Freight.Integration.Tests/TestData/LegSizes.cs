using Freight.Domain.Routing.Abstractions;

namespace Freight.Integration.Tests.TestData;

/// <summary>
/// The leg palette scenario routes are built from. One tick is 5 minutes of driving; the
/// tick count is what the simulation and the EU-561 rules consume. DistanceKm is only
/// plausible filler (~80 km/h) - no assertion uses it.
/// </summary>
public static class LegSizes
{
    /// <summary>1 hour of driving - the connecting drive between one shipment and the next.</summary>
    public static RouteLeg Hop { get; } = new(DistanceKm: 80, TimeTicks: 12);

    /// <summary>2 hours of driving.</summary>
    public static RouteLeg Small { get; } = new(DistanceKm: 160, TimeTicks: 24);

    /// <summary>11 hours of driving - more than one 9h driving day.</summary>
    public static RouteLeg Medium { get; } = new(DistanceKm: 880, TimeTicks: 132);

    /// <summary>24 hours of driving - several driving days with rest in between.</summary>
    public static RouteLeg Long { get; } = new(DistanceKm: 1920, TimeTicks: 288);
}
