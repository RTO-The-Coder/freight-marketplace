using Freight.Domain.Tracking;

namespace Freight.Domain.Tests;

public class RouteProgressTests
{
    [Fact]
    public void Constructor_ValidArguments_ExposesAllProperties()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 3600);

        Assert.Equal(100, progress.TotalDistanceKm);
        Assert.Equal(0, progress.CurrentDistanceKm);
        Assert.Equal(3600, progress.TotalTimeTick);
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
    public void UpdateProgress_SetsCurrentDistanceKm()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 3600);

        progress.UpdateProgress(40);

        Assert.Equal(40, progress.CurrentDistanceKm);
    }

    [Fact]
    public void UpdateProgress_NegativeDistance_Throws()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 3600);

        Assert.Throws<ArgumentOutOfRangeException>(() => progress.UpdateProgress(-1));
    }

    [Fact]
    public void IsLegComplete_BelowTotalDistance_ReturnsFalse()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 3600);
        progress.UpdateProgress(50);

        Assert.False(progress.IsLegComplete());
    }

    [Fact]
    public void IsLegComplete_AtOrAboveTotalDistance_ReturnsTrue()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 3600);
        progress.UpdateProgress(100);

        Assert.True(progress.IsLegComplete());
    }

    [Fact]
    public void StartNewLeg_ResetsCurrentDistanceAndSetsNewTotals()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 3600);
        progress.UpdateProgress(100);

        progress.StartNewLeg(totalDistanceKm: 50, totalTimeTick: 1800);

        Assert.Equal(50, progress.TotalDistanceKm);
        Assert.Equal(0, progress.CurrentDistanceKm);
        Assert.Equal(1800, progress.TotalTimeTick);
    }

    [Fact]
    public void GetProgressFraction_HalfwayThroughLeg_ReturnsOneHalf()
    {
        var progress = new RouteProgress(totalDistanceKm: 100, totalTimeTick: 3600);
        progress.UpdateProgress(50);

        Assert.Equal(0.5, progress.GetProgressFraction());
    }
}
