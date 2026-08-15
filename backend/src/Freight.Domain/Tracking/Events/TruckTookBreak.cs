using Freight.Domain.Common;

namespace Freight.Domain.Tracking.Events;

public sealed record TruckTookBreak(
    Guid DriverId,
    DateTime OccurredAt,
    BreakPreference VariantTaken,
    bool WasPolicyOverridden) : IDomainEvent;
