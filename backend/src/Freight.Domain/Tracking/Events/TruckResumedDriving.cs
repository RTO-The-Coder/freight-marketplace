using Freight.Domain.Common;

namespace Freight.Domain.Tracking.Events;

public sealed record TruckResumedDriving(
    Guid DriverId,
    DateTime OccurredAt) : IDomainEvent;
