using Freight.Domain.Geocoding.Abstractions;

namespace Freight.Api.Tests.TestSupport;

/// <summary>
/// Fixed <see cref="IGeocodingService"/> test double, registered in place of the real
/// Nominatim-backed service (see <see cref="ApiTestFactory"/>) so no test calls a live,
/// rate-limited third party. Returns <see cref="Matches"/> for every query.
/// </summary>
public sealed class FakeGeocodingService : IGeocodingService
{
    public IReadOnlyList<GeocodingMatch> Matches { get; set; } = [new("1100", "Vienna", "Austria", 48.155, 16.382)];

    /// <summary>When set, every call throws this - simulates Nominatim being down (mapped to HTTP 503).</summary>
    public GeocodingUnavailableException? ThrowOnCall { get; set; }

    public List<string> Queries { get; } = [];

    public Task<IReadOnlyList<GeocodingMatch>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        Queries.Add(query);
        return ThrowOnCall is not null ? throw ThrowOnCall : Task.FromResult(Matches);
    }
}
