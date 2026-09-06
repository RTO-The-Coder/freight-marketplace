using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record RescheduleTripRequest(Guid TripId, DateTime NewStartTime);

public sealed record RescheduleTripResponse(Guid TripId, DateTime StartedAt);

/// <summary>
/// Changes a not-yet-moved trip's planned departure (<c>Trip.StartedAt</c>) - the
/// dispatcher adjusting when a truck will leave, before it actually has. Rejected once the
/// trip has reached any stop or its truck has started driving (see
/// <see cref="Domain.Fleet.Trip.Reschedule"/>).
/// </summary>
public sealed class RescheduleTripHandler(IUnitOfWork unitOfWork)
{
    public async Task<RescheduleTripResponse> RescheduleTripAsync(
        RescheduleTripRequest request, CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetByIdAsync(request.TripId, cancellationToken)
            ?? throw new InvalidOperationException($"Trip '{request.TripId}' was not found.");

        var truck = await unitOfWork.Trucks.GetByIdAsync(trip.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{trip.TruckId}' for trip '{trip.Id}' was not found.");

        trip.Reschedule(request.NewStartTime, truckHasStartedDriving: truck.CurrentProgress is { } p && p.CurrentDrivingTimeTick > 0);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RescheduleTripResponse(trip.Id, trip.StartedAt);
    }
}
