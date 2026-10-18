using System.Diagnostics;
using Freight.Domain.Geocoding.Abstractions;
using Freight.Infrastructure.Geocoding;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Freight.Infrastructure.Tests;

/// <summary>
/// <see cref="ThrottlingGeocodingService"/>: repeated searches are answered from the cache, calls
/// that reach the provider are spaced apart, and failures are not cached. Runs with a short
/// interval so the test stays fast; the gate is process-wide, as in production.
/// </summary>
public sealed class ThrottlingGeocodingServiceTests
{
    private const int IntervalMilliseconds = 200;

    private sealed class CountingGeocoder(Func<string, IReadOnlyList<GeocodingMatch>> answer) : IGeocodingService
    {
        public List<string> Queries { get; } = [];

        public Task<IReadOnlyList<GeocodingMatch>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            Queries.Add(query);
            return Task.FromResult(answer(query));
        }
    }

    private static ThrottlingGeocodingService NewService(IGeocodingService inner) =>
        new(
            inner,
            Options.Create(new NominatimOptions { MinRequestIntervalMilliseconds = IntervalMilliseconds }),
            new MemoryCache(new MemoryCacheOptions()));

    private static readonly GeocodingMatch Vienna = new("1100", "Vienna", "Austria", 48.155, 16.382);

    [Fact]
    public async Task SearchAsync_SameQueryIgnoringCaseAndSpacing_IsAnsweredFromTheCache()
    {
        var inner = new CountingGeocoder(_ => [Vienna]);
        var service = NewService(inner);

        var first = await service.SearchAsync("1100 Favoriten");
        var second = await service.SearchAsync("  1100   FAVORITEN ");

        Assert.Single(inner.Queries);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task SearchAsync_DifferentQueries_AreSpacedAtLeastTheMinimumIntervalApart()
    {
        var inner = new CountingGeocoder(_ => []);
        var service = NewService(inner);

        await service.SearchAsync("1100 Favoriten");
        var stopwatch = Stopwatch.StartNew();
        await service.SearchAsync("80803 Schwabing");
        stopwatch.Stop();

        Assert.Equal(2, inner.Queries.Count);
        // A small tolerance for timer resolution.
        Assert.True(stopwatch.ElapsedMilliseconds >= IntervalMilliseconds - 20, $"second call waited only {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task SearchAsync_Failure_IsNotCached()
    {
        var calls = 0;
        var inner = new CountingGeocoder(_ => ++calls == 1 ? throw new GeocodingUnavailableException("down") : [Vienna]);
        var service = NewService(inner);

        await Assert.ThrowsAsync<GeocodingUnavailableException>(() => service.SearchAsync("1100 Favoriten"));
        var retry = await service.SearchAsync("1100 Favoriten");

        Assert.Equal([Vienna], retry);
        Assert.Equal(2, inner.Queries.Count);
    }
}
