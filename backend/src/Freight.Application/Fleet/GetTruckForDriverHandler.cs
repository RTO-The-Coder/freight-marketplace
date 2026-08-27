using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record GetTruckForDriverRequest(Guid DriverId);

public sealed record GetTruckForDriverResponse(TruckSummaryDto? Truck);

public sealed class GetTruckForDriverHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetTruckForDriverResponse> HandleAsync(GetTruckForDriverRequest request, CancellationToken cancellationToken = default)
    {
        var trucks = await unitOfWork.Trucks.GetAllAsync(cancellationToken);

        var truck = trucks.FirstOrDefault(t =>
            t.DriverAssignment is not null
            && (t.DriverAssignment.PrimaryDriver.Id == request.DriverId
                || t.DriverAssignment.SecondaryDriver?.Id == request.DriverId));

        if (truck is null)
        {
            return new GetTruckForDriverResponse(null);
        }

        var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);

        var dto = new TruckSummaryDto(
            truck.Id,
            truck.TruckName,
            truck.Type,
            truck.Size,
            truck.IsActive,
            truck.DetermineStatus(trip),
            truck.TruckingCompanyId,
            HasDriverAssignment: true);

        return new GetTruckForDriverResponse(dto);
    }
}
