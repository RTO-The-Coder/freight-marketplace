using Freight.Domain.Tracking;

namespace Freight.Domain.Tests;

public class RouteProgressTests
{
    [Fact]
    public void Constructor_ValidArguments_ExposesAllProperties()
    {
        var truckId = Guid.NewGuid();

        var progress = new RouteProgress(truckId, currentLegIndex: 2, ticksElapsedInCurrentLeg: 5);

        Assert.Equal(truckId, progress.TruckId);
        Assert.Equal(2, progress.CurrentLegIndex);
        Assert.Equal(5, progress.TicksElapsedInCurrentLeg);
    }

    [Fact]
    public void Constructor_Defaults_StartAtRouteBeginning()
    {
        var progress = new RouteProgress(Guid.NewGuid());

        Assert.Equal(0, progress.CurrentLegIndex);
        Assert.Equal(0, progress.TicksElapsedInCurrentLeg);
    }

    [Fact]
    public void Constructor_EmptyTruckId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new RouteProgress(Guid.Empty));
    }

    [Fact]
    public void Constructor_NegativeCurrentLegIndex_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RouteProgress(Guid.NewGuid(), currentLegIndex: -1));
    }

    [Fact]
    public void Constructor_NegativeTicksElapsedInCurrentLeg_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RouteProgress(Guid.NewGuid(), ticksElapsedInCurrentLeg: -1));
    }
}
