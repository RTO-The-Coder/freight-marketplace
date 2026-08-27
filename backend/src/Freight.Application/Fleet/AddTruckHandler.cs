using Freight.Domain.Common;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects;

namespace Freight.Application.Fleet;

public sealed record AddTruckRequest(string TruckName, TruckType TruckType, TruckSize TruckSize, Guid? TruckingCompanyId = null);

public sealed record AddTruckResponse(Guid TruckId, string TruckName, TruckType TruckType, TruckSize TruckSize, Capacity TruckCapacity, bool IsActive, Guid? TruckingCompanyId = null);

public sealed class AddTruckHandler(IUnitOfWork unitOfWork)
{
    public async Task<AddTruckResponse> AddTruckAsync(AddTruckRequest request, CancellationToken cancellationToken = default)
    {
        var truck = Truck.Create(request.TruckName, request.TruckType, request.TruckSize);

        if (request.TruckingCompanyId is { } companyId)
        {
            truck.AssignToCompany(companyId);
        }

        unitOfWork.Trucks.Add(truck);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddTruckResponse(truck.Id, truck.TruckName, truck.Type, truck.Size, truck.Capacity, truck.IsActive, truck.TruckingCompanyId);
    }
}
