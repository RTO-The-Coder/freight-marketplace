using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record AssignTruckToCompanyRequest(Guid TruckId, Guid TruckingCompanyId);

public sealed class AssignTruckToCompanyHandler(IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(AssignTruckToCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        truck.AssignToCompany(request.TruckingCompanyId);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
