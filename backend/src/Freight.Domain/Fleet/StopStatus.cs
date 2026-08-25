namespace Freight.Domain.Fleet;

/// <summary>
/// Whether a <see cref="Stop"/> has been reached yet. Replaces deletion - a Stop is
/// created Pending and flips to Reached in place; the row is never removed.
/// </summary>
public enum StopStatus
{
    Pending,
    Reached,
}
