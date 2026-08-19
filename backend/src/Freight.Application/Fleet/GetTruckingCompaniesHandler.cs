using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record TruckingCompanySummaryDto(Guid CompanyId, string Name);

public sealed record GetTruckingCompaniesResponse(IReadOnlyList<TruckingCompanySummaryDto> Companies);

public sealed class GetTruckingCompaniesHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetTruckingCompaniesResponse> HandleAsync(CancellationToken cancellationToken = default)
    {
        var companies = await unitOfWork.TruckingCompanies.GetAllAsync(cancellationToken);
        var dtos = companies.Select(c => new TruckingCompanySummaryDto(c.Id, c.Name)).ToList();
        return new GetTruckingCompaniesResponse(dtos);
    }
}
