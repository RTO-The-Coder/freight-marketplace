using Freight.Domain.Common;
using Freight.Domain.Tracking.Enums;

namespace Freight.Domain.Tracking.Events;

public sealed record TruckWentIntoRest(
    Guid DriverId,
    DateTime OccurredAt,
    DriverActivity RestType,
    bool WasPolicyOverridden) : IDomainEvent;
