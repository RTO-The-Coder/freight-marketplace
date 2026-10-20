using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Freight.Domain.Routing.Abstractions;
using Freight.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace Freight.Infrastructure.Routing;

/// <summary>
/// <see cref="IRoutingService"/> backed by OSRM's HTTP <c>route</c> API. One call per
/// leg: <c>GET {base}/route/v1/driving/{lon1},{lat1};{lon2},{lat2}?...</c>, reading the
/// single returned route's distance (metres) and duration (seconds), plus - for
/// <see cref="GetRouteGeometryAsync"/> - its GeoJSON line geometry.
///
/// Duration is converted to whole 5-minute ticks, always rounded UP, so a route walk
/// that only adds these never under-predicts an arrival. Any transport failure,
/// non-success status, or an OSRM response that isn't a usable route becomes
/// <see cref="RoutingUnavailableException"/> - Phase 1 has no fallback distance source
/// (ADR 0011).
///
/// A temporary server error (502/503/504, a timeout or a connection error) is retried
/// <see cref="OsrmOptions.RetryCount"/> more times before giving up, waiting
/// <see cref="OsrmOptions.RetryDelayMilliseconds"/> and doubling it each time. Retries run
/// inside the <see cref="ThrottlingRoutingService"/> gate, so each wait is at least
/// <see cref="OsrmOptions.MinRequestIntervalMilliseconds"/> to keep the throttle's spacing.
/// </summary>
public sealed class OsrmRoutingService : IRoutingService
{
    private const int SecondsPerTick = 300;

    private readonly HttpClient _httpClient;
    private readonly OsrmOptions _options;

    public OsrmRoutingService(HttpClient httpClient, IOptions<OsrmOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options.Value;

        // The public OSRM demo server rejects requests with no User-Agent (403), which is
        // what HttpClient sends by default. Set one unless the caller already configured it.
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("freight-marketplace/1.0 (+https://github.com/; portfolio demo)");
        }
    }

    public async Task<RouteLeg> GetRouteAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default)
    {
        var route = await RequestRouteAsync(from, to, geometry: false, cancellationToken);
        return new RouteLeg(route.Distance / 1000.0, ToTicks(route.Duration));
    }

    public async Task<RouteGeometry> GetRouteGeometryAsync(GeoLocation from, GeoLocation to, CancellationToken cancellationToken = default)
    {
        var route = await RequestRouteAsync(from, to, geometry: true, cancellationToken);

        // GeoJSON coordinates are [longitude, latitude] pairs - swap to (lat, lng).
        var path = (route.Geometry?.Coordinates ?? [])
            .Where(pair => pair.Count >= 2)
            .Select(pair => new GeoPoint(pair[1], pair[0]))
            .ToList();

        return new RouteGeometry(route.Distance / 1000.0, ToTicks(route.Duration), path);
    }

    private static int ToTicks(double seconds) => Math.Max(0, (int)Math.Ceiling(seconds / SecondsPerTick));

    private async Task<OsrmRoute> RequestRouteAsync(
        GeoLocation from, GeoLocation to, bool geometry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        // OSRM wants lon,lat pairs with '.' decimal separators - format each coordinate
        // explicitly under the invariant culture so a server locale never turns 13.405
        // into "13,405" (which OSRM reads as two arguments and rejects with 400/403).
        static string Coord(double value) => value.ToString("0.0#####", CultureInfo.InvariantCulture);

        var query = geometry ? "overview=full&geometries=geojson" : "overview=false";
        var path =
            $"route/v1/driving/{Coord(from.Longitude)},{Coord(from.Latitude)};" +
            $"{Coord(to.Longitude)},{Coord(to.Latitude)}?{query}";

        var legLabel = $"({Coord(from.Latitude)},{Coord(from.Longitude)}) -> ({Coord(to.Latitude)},{Coord(to.Longitude)})";

        OsrmRouteResponse? payload;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                payload = await SendOnceAsync(path, legLabel, cancellationToken);
                break;
            }
            catch (TransientOsrmException) when (attempt < _options.RetryCount)
            {
                await Task.Delay(RetryDelay(attempt), cancellationToken);
            }
            catch (TransientOsrmException ex)
            {
                var attempts = attempt + 1;
                var message = $"{ex.Message} ({attempts} attempt{(attempts == 1 ? "" : "s")})";
                throw ex.InnerException is { } inner
                    ? new RoutingUnavailableException(message, inner)
                    : new RoutingUnavailableException(message);
            }
        }

        if (payload is null || !string.Equals(payload.Code, "Ok", StringComparison.Ordinal)
            || payload.Routes is not { Count: > 0 })
        {
            throw new RoutingUnavailableException(
                $"OSRM found no route for leg {legLabel} (code: {payload?.Code ?? "none"}).");
        }

        return payload.Routes[0];
    }

    /// <summary>
    /// One HTTP attempt. Temporary failures throw <see cref="TransientOsrmException"/> (the
    /// caller retries them); anything else throws <see cref="RoutingUnavailableException"/>.
    /// </summary>
    private async Task<OsrmRouteResponse?> SendOnceAsync(string path, string legLabel, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(path, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var message = $"OSRM returned {(int)response.StatusCode} {response.ReasonPhrase} for leg {legLabel}.";
                if (response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout)
                {
                    throw new TransientOsrmException(message);
                }

                throw new RoutingUnavailableException(message);
            }

            return await response.Content.ReadFromJsonAsync<OsrmRouteResponse>(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Connection error, or HttpClient's own timeout (not the caller cancelling).
            throw new TransientOsrmException($"OSRM was unreachable for leg {legLabel}: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is TaskCanceledException or JsonException)
        {
            throw new RoutingUnavailableException(
                $"OSRM was unreachable for leg {legLabel}: {ex.Message}", ex);
        }
    }

    private TimeSpan RetryDelay(int attempt)
    {
        var backoff = _options.RetryDelayMilliseconds * Math.Pow(2, attempt);
        return TimeSpan.FromMilliseconds(Math.Max(backoff, _options.MinRequestIntervalMilliseconds));
    }

    private sealed class TransientOsrmException(string message, Exception? inner = null) : Exception(message, inner);

    private sealed record OsrmRouteResponse(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("routes")] IReadOnlyList<OsrmRoute>? Routes);

    private sealed record OsrmRoute(
        [property: JsonPropertyName("distance")] double Distance,
        [property: JsonPropertyName("duration")] double Duration,
        [property: JsonPropertyName("geometry")] OsrmGeometry? Geometry = null);

    private sealed record OsrmGeometry(
        [property: JsonPropertyName("coordinates")] IReadOnlyList<IReadOnlyList<double>>? Coordinates);
}
