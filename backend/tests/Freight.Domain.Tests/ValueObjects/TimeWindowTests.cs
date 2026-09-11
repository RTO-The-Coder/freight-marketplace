using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests.ValueObjects;

public class TimeWindowTests
{
    private static readonly DateTime Base = new(2026, 1, 1, 8, 0, 0);

    [Fact]
    public void Create_EarliestBeforeLatest_SetsProperties()
    {
        var latest = Base.AddHours(2);

        var window = TimeWindow.Create(Base, latest);

        Assert.Equal(Base, window.Earliest);
        Assert.Equal(latest, window.Latest);
    }

    [Fact]
    public void Create_EarliestEqualsLatest_Throws()
    {
        Assert.Throws<ArgumentException>(() => TimeWindow.Create(Base, Base));
    }

    [Fact]
    public void Create_EarliestAfterLatest_Throws()
    {
        Assert.Throws<ArgumentException>(() => TimeWindow.Create(Base, Base.AddMinutes(-1)));
    }

    [Fact]
    public void Create_OneTickBeforeLatest_Succeeds()
    {
        var latest = Base.AddTicks(1);

        var window = TimeWindow.Create(Base, latest);

        Assert.Equal(Base, window.Earliest);
        Assert.Equal(latest, window.Latest);
    }
}
