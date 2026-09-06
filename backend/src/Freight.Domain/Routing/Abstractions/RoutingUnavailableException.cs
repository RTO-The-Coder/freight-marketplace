namespace Freight.Domain.Routing.Abstractions;

/// <summary>
/// The routing provider could not answer - unreachable, timed out, errored, or no route
/// exists. Phase 1 has no fallback distance source (ADR 0011), so any operation needing a
/// real leg fails rather than continuing on a guess. Surfaces as HTTP 503.
/// </summary>
public sealed class RoutingUnavailableException : Exception
{
    public RoutingUnavailableException(string message)
        : base(message)
    {
    }

    public RoutingUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
