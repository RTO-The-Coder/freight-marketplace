using Freight.Domain.ValueObjects;

namespace Freight.Integration.Tests.TestData;

/// <summary>
/// Named, fixed locations for scenario routes. The coordinates mean nothing - no assertion
/// depends on geography, and leg durations come from the scenario's route table, not from
/// distance. They only need to be distinct so each (from, to) pair is a unique key.
/// </summary>
public static class TestPoints
{
    public static GeoLocation Office { get; } = GeoLocation.Create(50.000000, 8.000000);

    public static GeoLocation P1 { get; } = P(1);
    public static GeoLocation P2 { get; } = P(2);
    public static GeoLocation P3 { get; } = P(3);
    public static GeoLocation P4 { get; } = P(4);
    public static GeoLocation P5 { get; } = P(5);
    public static GeoLocation P6 { get; } = P(6);
    public static GeoLocation P7 { get; } = P(7);
    public static GeoLocation P8 { get; } = P(8);

    /// <summary>Point n (n = 1, 2, ...) for longer routes; P1 - P8 above are P(1) - P(8).</summary>
    public static GeoLocation P(int n) => GeoLocation.Create(50 + n / 10.0, 8 + n / 10.0);

    /// <summary>"P1" ... "P<paramref name="n"/>" - the stops reached so far on a route in point order.</summary>
    public static string[] UpTo(int n) => [.. Enumerable.Range(1, n).Select(i => $"P{i}")];

    /// <summary>"P1" ... "P<paramref name="n"/>", "Office" - every stop, trip over.</summary>
    public static string[] AllAndOffice(int n) => [.. UpTo(n), "Office"];

    /// <summary>"Office" and P1 ... P<paramref name="count"/>, for naming stops in assertions.</summary>
    public static (string Name, GeoLocation Location)[] Named(int count) =>
        [("Office", Office), .. Enumerable.Range(1, count).Select(n => ($"P{n}", P(n)))];
}
