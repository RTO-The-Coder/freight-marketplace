using Freight.Domain.Common;

namespace Freight.Domain.Tracking.Events;

public sealed record TruckWentIntoRest(
    Guid DriverId,
    DateTime OccurredAt,
    DriverActivity RestType,
    bool WasPolicyOverridden) : IDomainEvent;
