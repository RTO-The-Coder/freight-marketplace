using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record UnassignTruckFromCompanyRequest(Guid TruckId);

public sealed class UnassignTruckFromCompanyHandler(IUnitOfWork unitOfWork)
{
    public async Task HandleAsync(UnassignTruckFromCompanyRequest request, CancellationToken cancellationToken = default)
    {
        var truck = await unitOfWork.Trucks.GetByIdAsync(request.TruckId, cancellationToken)
            ?? throw new InvalidOperationException($"Truck '{request.TruckId}' was not found.");

        truck.UnassignFromCompany();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
