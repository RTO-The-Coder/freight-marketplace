namespace Freight.Application.Tests;

/// <summary>Minimal fixed-instant TimeProvider test double, for handlers that fall back to it before a SimulationClock exists.</summary>
public sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
