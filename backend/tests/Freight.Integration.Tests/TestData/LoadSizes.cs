using Freight.Domain.ValueObjects;

namespace Freight.Integration.Tests.TestData;

/// <summary>
/// Shipment loads, sized against a Medium truck's 9,000 kg / 45 m³. Any single load fits;
/// when loads overlap on board, Heavy + Medium fills the truck exactly and Heavy + Heavy
/// exceeds it.
/// </summary>
public static class LoadSizes
{
    public static Capacity Small { get; } = Capacity.Create(1_000, 5);
    public static Capacity Medium { get; } = Capacity.Create(3_000, 15);
    public static Capacity Heavy { get; } = Capacity.Create(6_000, 30);
}
