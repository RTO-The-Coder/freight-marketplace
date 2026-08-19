using Freight.Domain.Common;
using Freight.Domain.Fleet;

namespace Freight.Application.Fleet;

public sealed record AddTruckRequest(string TruckName, TruckType TruckType, TruckSize TruckSize, Guid? TruckingCompanyId = null);

public sealed record AddTruckResponse(Guid TruckId);

public sealed class AddTruckHandler(IUnitOfWork unitOfWork)
{
    public async Task<AddTruckResponse> HandleAsync(AddTruckRequest request, CancellationToken cancellationToken = default)
    {
        var truck = Truck.Create(request.TruckName, request.TruckType, request.TruckSize);

        if (request.TruckingCompanyId is { } companyId)
        {
            truck.AssignToCompany(companyId);
        }

        unitOfWork.Trucks.Add(truck);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddTruckResponse(truck.Id);
    }
}
