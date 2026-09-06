using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Freight.Infrastructure.Routing;

/// <summary>
/// Wraps an inner <see cref="IRoutingService"/> and spaces its calls at least
/// <see cref="OsrmOptions.MinRequestIntervalMilliseconds"/> apart, process-wide. The
/// public OSRM demo server asks for no more than ~1 request/second and can withdraw
/// access if that is abused; an assignment makes several leg calls, and concurrent
/// assignments would otherwise burst past the limit.
///
/// The gate is a single static <see cref="SemaphoreSlim"/> and last-call timestamp, so
/// every scoped instance shares one queue for the lifetime of the process. Registered as
/// the outermost <see cref="IRoutingService"/>, in front of <see cref="OsrmRoutingService"/>.
/// </summary>
public sealed class ThrottlingRoutingService(IRoutingService inner, IOptions<OsrmOptions> options) : IRoutingService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTimeOffset _lastCallCompletedAt = DateTimeOffset.MinValue;

    private readonly TimeSpan _minInterval = TimeSpan.FromMilliseconds(options.Value.MinRequestIntervalMilliseconds);

    public Task<RouteLeg> GetRouteAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default) =>
        ThrottledAsync(ct => inner.GetRouteAsync(from, to, ct), cancellationToken);

    public Task<RouteGeometry> GetRouteGeometryAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default) =>
        ThrottledAsync(ct => inner.GetRouteGeometryAsync(from, to, ct), cancellationToken);

    private async Task<T> ThrottledAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            var sinceLast = DateTimeOffset.UtcNow - _lastCallCompletedAt;
            if (sinceLast < _minInterval)
            {
                await Task.Delay(_minInterval - sinceLast, cancellationToken);
            }

            return await call(cancellationToken);
        }
        finally
        {
            _lastCallCompletedAt = DateTimeOffset.UtcNow;
            Gate.Release();
        }
    }
}
