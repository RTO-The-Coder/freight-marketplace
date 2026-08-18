using Freight.Domain.Common;
using Freight.Domain.ValueObjects.RuleVariants;

namespace Freight.Domain.Tracking.Events;

public sealed record TruckTookBreak(
    Guid DriverId,
    DateTime OccurredAt,
    DrivingBreakRule VariantTaken,
    bool WasPolicyOverridden) : IDomainEvent;
