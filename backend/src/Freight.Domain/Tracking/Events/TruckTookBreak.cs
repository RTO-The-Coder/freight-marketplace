using Freight.Domain.Common;
using Freight.Domain.ValueObjects.DrivingRules;

namespace Freight.Domain.Tracking.Events;

public sealed record TruckTookBreak(
    Guid DriverId,
    DateTime OccurredAt,
    DrivingBreakRule VariantTaken,
    bool WasPolicyOverridden) : IDomainEvent;
