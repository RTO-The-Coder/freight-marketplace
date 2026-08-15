using Freight.Domain.Common;

namespace Freight.Domain.Tracking.Events;

public sealed record TruckArrivedAtDestination(
    Guid DriverId,
    DateTime OccurredAt) : IDomainEvent;
