using Freight.Domain.Common;
using Freight.Domain.Fleet;

namespace Freight.Application.Fleet;

public sealed record GetTrucksRequest(bool UnassignedOnly, Guid? TruckingCompanyId = null);

public sealed record TruckSummaryDto(
    Guid TruckId,
    string TruckName,
    TruckType TruckType,
    TruckSize TruckSize,
    bool IsActive,
    TruckStatus Status,
    Guid? TruckingCompanyId,
    bool HasDriverAssignment);

public sealed record GetTrucksResponse(IReadOnlyList<TruckSummaryDto> Trucks);

public sealed class GetTrucksHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetTrucksResponse> HandleAsync(GetTrucksRequest request, CancellationToken cancellationToken = default)
    {
        var trucks = await unitOfWork.Trucks.GetAllAsync(cancellationToken);

        IEnumerable<Truck> filtered = trucks;
        if (request.TruckingCompanyId is { } companyId)
        {
            filtered = filtered.Where(truck => truck.TruckingCompanyId == companyId);
        }
        else if (request.UnassignedOnly)
        {
            filtered = filtered.Where(truck => truck.TruckingCompanyId is null);
        }

        var dtos = new List<TruckSummaryDto>();

        foreach (var truck in filtered)
        {
            var trip = await unitOfWork.Trips.GetOpenTripByTruckIdAsync(truck.Id, cancellationToken);

            dtos.Add(new TruckSummaryDto(
                truck.Id,
                truck.TruckName,
                truck.Type,
                truck.Size,
                truck.IsActive,
                truck.DetermineStatus(trip),
                truck.TruckingCompanyId,
                truck.DriverAssignment is not null));
        }

        return new GetTrucksResponse(dtos);
    }
}
