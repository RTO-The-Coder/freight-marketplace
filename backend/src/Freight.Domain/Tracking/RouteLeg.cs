namespace Freight.Domain.Tracking;

public sealed record RouteLeg(Guid TruckId, int LegIndex, int DurationTicks);
