using Freight.Domain.Fleet.Enums;

namespace Freight.Domain.Fleet.ValueObjects;

/// <summary>
/// Why a truck is <see cref="TruckStatus.Parked"/>: it has reached the stop
/// <see cref="StopId"/> but that stop's time window does not open until
/// <see cref="WindowOpensAt"/>. Derived on read, never stored.
/// </summary>
public sealed record WaitingInfo(Guid StopId, StopKind StopKind, DateTime WindowOpensAt);
