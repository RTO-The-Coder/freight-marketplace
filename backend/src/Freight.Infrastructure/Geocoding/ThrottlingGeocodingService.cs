using Freight.Domain.Geocoding.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Freight.Infrastructure.Geocoding;

/// <summary>
/// Wraps an inner <see cref="IGeocodingService"/> with an in-process cache and a process-wide
/// throttle. Nominatim's usage policy allows at most 1 request/second and asks clients to
/// cache: a repeated search (same text, ignoring case and spacing) is answered from
/// <see cref="IMemoryCache"/>, and calls that do reach Nominatim are spaced at least
/// <see cref="NominatimOptions.MinRequestIntervalMilliseconds"/> apart.
///
/// The gate is a single static <see cref="SemaphoreSlim"/> and last-call timestamp, so every
/// scoped instance shares one queue for the lifetime of the process - the same design as
/// <c>ThrottlingRoutingService</c>. Failures are not cached.
/// </summary>
public sealed class ThrottlingGeocodingService(
    IGeocodingService inner, IOptions<NominatimOptions> options, IMemoryCache cache) : IGeocodingService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastCallCompletedAt = DateTimeOffset.MinValue;

    private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(options.Value.MinRequestIntervalMilliseconds);

    public async Task<IReadOnlyList<GeocodingMatch>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var key = $"geocode:{Normalize(query)}";
        if (cache.TryGetValue(key, out IReadOnlyList<GeocodingMatch>? cached) && cached is not null)
        {
            return cached;
        }

        await Gate.WaitAsync(cancellationToken);
        try
        {
            var sinceLast = DateTimeOffset.UtcNow - _lastCallCompletedAt;
            if (sinceLast < _minInterval)
            {
                await Task.Delay(_minInterval - sinceLast, cancellationToken);
            }

            var matches = await inner.SearchAsync(query, cancellationToken);
            cache.Set(key, matches, CacheDuration);
            return matches;
        }
        finally
        {
            _lastCallCompletedAt = DateTimeOffset.UtcNow;
            Gate.Release();
        }
    }

    /// <summary>Lower case, single spaces, trimmed - "1100  Favoriten " and "1100 favoriten" share one entry.</summary>
    private static string Normalize(string query) =>
        string.Join(' ', query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
}
