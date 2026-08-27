using System.Diagnostics;
using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record SetTruckCompanyRequest(Guid TruckId, Guid? TruckingCompanyId);

public sealed class SetTruckCompanyHandler(IUnitOfWork unitOfWork)
{
    public async Task AssignmentTruckingCompany(SetTruckCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken) ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        if (request.TruckingCompanyId is { } companyId)
            truck.AssignToCompany(companyId);
        else
            truck.UnassignFromCompany();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
