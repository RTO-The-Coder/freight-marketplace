using Freight.Domain.Geocoding.Abstractions;

namespace Freight.Integration.Tests.TestSupport;

/// <summary>
/// Stand-in for address search: the scenarios book with fixed coordinates and never search,
/// so this only guarantees no test can reach the live Nominatim server. Finds nothing.
/// </summary>
internal sealed class NoGeocodingService : IGeocodingService
{
    public Task<IReadOnlyList<GeocodingMatch>> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GeocodingMatch>>([]);
}
