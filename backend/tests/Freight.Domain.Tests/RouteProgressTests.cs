using Freight.Domain.Tracking;

namespace Freight.Domain.Tests;

public class RouteProgressTests
{
    [Fact]
    public void Constructor_ValidArguments_ExposesAllProperties()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 78);

        Assert.Equal(100, progress.TotalDistanceKm);
        Assert.Equal(0, progress.CurrentDistanceKm);
        Assert.Equal(0, progress.CurrentDrivingTimeTick);
        Assert.Equal(78, progress.TotalTimeTick);
    }

    [Fact]
    public void Constructor_NegativeTotalDistanceKm_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RouteProgress(totalDistanceKm: -1, totalTimeTick: 0));
    }

    [Fact]
    public void Constructor_NegativeTotalTimeTick_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RouteProgress(totalDistanceKm: 0, totalTimeTick: -1));
    }

    [Fact]
    public void AdvanceByTicks_SetsCurrentDrivingTimeTickAndDerivesDistance()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 78);

        progress.AdvanceByTicks(39);

        Assert.Equal(39, progress.CurrentDrivingTimeTick);
        Assert.Equal(50, progress.CurrentDistanceKm);
    }

    [Fact]
    public void AdvanceByTicks_Accumulates()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 78);

        progress.AdvanceByTicks(20);
        progress.AdvanceByTicks(19);

        Assert.Equal(39, progress.CurrentDrivingTimeTick);
    }

    [Fact]
    public void AdvanceByTicks_NegativeTicks_Throws()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 78);

        Assert.Throws<ArgumentOutOfRangeException>(() => progress.AdvanceByTicks(-1));
    }

    [Fact]
    public void AdvanceByTicks_BeyondTotalTimeTick_ClampsAtTotal()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 78);

        progress.AdvanceByTicks(200);

        Assert.Equal(78, progress.CurrentDrivingTimeTick);
        Assert.Equal(100, progress.CurrentDistanceKm);
    }

    [Fact]
    public void IsLegComplete_BelowTotalTimeTick_ReturnsFalse()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 78);
        progress.AdvanceByTicks(39);

        Assert.False(progress.IsLegComplete());
    }

    [Fact]
    public void IsLegComplete_AtOrAboveTotalTimeTick_ReturnsTrue()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 78);
        progress.AdvanceByTicks(78);

        Assert.True(progress.IsLegComplete());
    }

    [Fact]
    public void StartNewLeg_ResetsCurrentDrivingTimeTickAndSetsNewTotals()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 78);
        progress.AdvanceByTicks(78);

        progress.StartNewLeg(totalDistanceKm: 50, totalTimeTick: 40);

        Assert.Equal(50, progress.TotalDistanceKm);
        Assert.Equal(0, progress.CurrentDrivingTimeTick);
        Assert.Equal(0, progress.CurrentDistanceKm);
        Assert.Equal(40, progress.TotalTimeTick);
    }

    [Fact]
    public void GetProgressFraction_HalfwayThroughLeg_ReturnsOneHalf()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 78);
        progress.AdvanceByTicks(39);

        Assert.Equal(0.5, progress.GetProgressFraction());
    }
}
