using Freight.Domain.Tracking;

namespace Freight.Domain.Tests.Tracking;

public class RouteProgressTests
{
    [Fact]
    public void Constructor_ValidInput_SetsTotalsAndZeroesCurrentTick()
    {
        var progress = new RouteProgress(100, 20);

        Assert.Equal(100, progress.TotalDistanceKm);
        Assert.Equal(20, progress.TotalTimeTick);
        Assert.Equal(0, progress.CurrentDrivingTimeTick);
        Assert.Equal(0, progress.CurrentDistanceKm);
    }

    [Fact]
    public void Constructor_NegativeDistance_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RouteProgress(-1, 20));
    }

    [Fact]
    public void Constructor_NegativeTimeTick_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RouteProgress(100, -1));
    }

    [Fact]
    public void AdvanceByTicks_PartialProgress_UpdatesCurrentDistanceProportionally()
    {
        var progress = new RouteProgress(100, 20);

        progress.AdvanceByTicks(5);

        Assert.Equal(5, progress.CurrentDrivingTimeTick);
        Assert.Equal(25, progress.CurrentDistanceKm);
        Assert.Equal(0.25, progress.GetProgressFraction());
    }

    [Fact]
    public void AdvanceByTicks_NegativeTicks_Throws()
    {
        var progress = new RouteProgress(100, 20);

        Assert.Throws<ArgumentOutOfRangeException>(() => progress.AdvanceByTicks(-1));
    }

    [Fact]
    public void AdvanceByTicks_BeyondTotal_ClampsAtTotal()
    {
        var progress = new RouteProgress(100, 20);

        progress.AdvanceByTicks(25);

        Assert.Equal(20, progress.CurrentDrivingTimeTick);
        Assert.True(progress.IsLegComplete());
    }

    [Fact]
    public void AdvanceByTicks_AcrossMultipleCalls_Accumulates()
    {
        var progress = new RouteProgress(100, 20);

        progress.AdvanceByTicks(5);
        progress.AdvanceByTicks(3);

        Assert.Equal(8, progress.CurrentDrivingTimeTick);
    }

    [Fact]
    public void IsLegComplete_OneTickShortOfTotal_IsFalse()
    {
        var progress = new RouteProgress(100, 20);

        progress.AdvanceByTicks(19);

        Assert.False(progress.IsLegComplete());
    }

    [Fact]
    public void IsLegComplete_ExactlyAtTotal_IsTrue()
    {
        var progress = new RouteProgress(100, 20);

        progress.AdvanceByTicks(20);

        Assert.True(progress.IsLegComplete());
    }

    [Fact]
    public void GetProgressFraction_ZeroLengthLeg_ReturnsOneInsteadOfDividingByZero()
    {
        var progress = new RouteProgress(0, 0);

        Assert.Equal(1.0, progress.GetProgressFraction());
        Assert.True(progress.IsLegComplete());
    }

    [Fact]
    public void StartNewLeg_MidProgress_ResetsCurrentTickAndReplacesTotals()
    {
        var progress = new RouteProgress(100, 20);
        progress.AdvanceByTicks(10);

        progress.StartNewLeg(50, 10);

        Assert.Equal(50, progress.TotalDistanceKm);
        Assert.Equal(10, progress.TotalTimeTick);
        Assert.Equal(0, progress.CurrentDrivingTimeTick);
    }

    [Fact]
    public void StartNewLeg_NegativeDistance_Throws()
    {
        var progress = new RouteProgress(100, 20);

        Assert.Throws<ArgumentOutOfRangeException>(() => progress.StartNewLeg(-1, 10));
    }

    [Fact]
    public void StartNewLeg_NegativeTimeTick_Throws()
    {
        var progress = new RouteProgress(100, 20);

        Assert.Throws<ArgumentOutOfRangeException>(() => progress.StartNewLeg(50, -1));
    }
}
