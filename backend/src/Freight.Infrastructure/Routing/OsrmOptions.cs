namespace Freight.Infrastructure.Routing;

/// <summary>
/// Configuration for <see cref="OsrmRoutingService"/>, bound from the "Routing" section.
/// Development points at the free public OSRM demo server; production self-hosts OSRM at
/// the same API shape, so switching is a <see cref="BaseUrl"/> change only (ADR 0011).
/// </summary>
public sealed class OsrmOptions
{
    public const string SectionName = "Routing";

    /// <summary>Root of the OSRM HTTP API, e.g. <c>https://router.project-osrm.org</c>.</summary>
    public string BaseUrl { get; set; } = "https://router.project-osrm.org";

    /// <summary>How long to wait for a single routing call before treating it as unavailable.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Minimum gap between consecutive routing calls, enforced process-wide by
    /// <see cref="ThrottlingRoutingService"/>. The public demo server asks for no more
    /// than ~1 request/second; a small margin over 1s keeps well inside that.
    /// </summary>
    public int MinRequestIntervalMilliseconds { get; set; } = 1100;

    /// <summary>
    /// How many more times <see cref="OsrmRoutingService"/> tries a leg after a temporary
    /// server error (502/503/504, timeout, connection error). The public demo server is
    /// intermittently overloaded; a second or third attempt usually succeeds.
    /// </summary>
    public int RetryCount { get; set; } = 2;

    /// <summary>
    /// Wait before the first retry; it doubles for each further retry (1 s, then 2 s).
    /// Never shorter than <see cref="MinRequestIntervalMilliseconds"/>, so retries keep the
    /// throttle's spacing.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 1000;
}
