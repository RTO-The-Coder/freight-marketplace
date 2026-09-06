using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record GetTruckingCompanyByIdRequest(Guid CompanyId);

/// <summary>
/// A single trucking company by id - name and office location, the same
/// <see cref="TruckingCompanySummaryDto"/> the list endpoint returns. Lets a client that
/// already knows a company id (a deep link, a stored selection) fetch just that one
/// instead of pulling the whole list and filtering.
/// </summary>
public sealed class GetTruckingCompanyByIdHandler(IUnitOfWork unitOfWork)
{
    public async Task<TruckingCompanySummaryDto> GetTruckingCompanybyIdAsync(GetTruckingCompanyByIdRequest request, CancellationToken cancellationToken = default)
    {
        var company = await unitOfWork.TruckingCompanies.GetByIdAsync(request.CompanyId, cancellationToken)
            ?? throw new InvalidOperationException($"Trucking company '{request.CompanyId}' was not found.");

        return GetTruckingCompaniesHandler.ToDto(company);
    }
}
