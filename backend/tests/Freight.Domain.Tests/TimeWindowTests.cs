using Freight.Domain.ValueObjects;

namespace Freight.Domain.Tests;

public class TimeWindowTests
{
    [Fact]
    public void Create_EarliestBeforeLatest_SetsProperties()
    {
        var earliest = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var latest = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc);

        var window = TimeWindow.Create(earliest, latest);

        Assert.Equal(earliest, window.Earliest);
        Assert.Equal(latest, window.Latest);
    }

    [Fact]
    public void Create_EarliestEqualsLatest_Throws()
    {
        var same = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => TimeWindow.Create(same, same));
    }

    [Fact]
    public void Create_EarliestAfterLatest_Throws()
    {
        var earliest = new DateTime(2026, 1, 1, 14, 0, 0, DateTimeKind.Utc);
        var latest = new DateTime(2026, 1, 1, 8, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => TimeWindow.Create(earliest, latest));
    }
}
