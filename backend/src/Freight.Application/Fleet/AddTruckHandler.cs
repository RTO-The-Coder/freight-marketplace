using Freight.Domain.Common;
using Freight.Domain.Fleet;

namespace Freight.Application.Fleet;

public sealed record AddTruckRequest(Guid TruckingCompanyId, string TruckName, TruckType TruckType, TruckSize TruckSize);

public sealed record AddTruckResponse(Guid TruckId);

public sealed class AddTruckHandler(IUnitOfWork unitOfWork)
{
    public async Task<AddTruckResponse> HandleAsync(AddTruckRequest request, CancellationToken cancellationToken = default)
    {
        var truck = Truck.Create(request.TruckName, request.TruckType, request.TruckSize);
        truck.AssignToCompany(request.TruckingCompanyId);

        unitOfWork.Trucks.Add(truck);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddTruckResponse(truck.Id);
    }
}
