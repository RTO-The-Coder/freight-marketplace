using Freight.Domain.Common;

namespace Freight.Application.Fleet;

public sealed record TruckingCompanySummaryDto(
    Guid CompanyId,
    string Name,
    double OfficeLatitude,
    double OfficeLongitude);

public sealed record GetTruckingCompaniesResponse(IReadOnlyList<TruckingCompanySummaryDto> Companies);

public sealed class GetTruckingCompaniesHandler(IUnitOfWork unitOfWork)
{
    public async Task<GetTruckingCompaniesResponse> GetTruckingCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var companies = await unitOfWork.TruckingCompanies.GetAllAsync(cancellationToken);
        var dtos = companies.Select(ToDto).ToList();
        return new GetTruckingCompaniesResponse(dtos);
    }

    internal static TruckingCompanySummaryDto ToDto(Domain.Fleet.TruckingCompany company) =>
        new(company.Id, company.Name, company.OfficeLocation.Latitude, company.OfficeLocation.Longitude);
}
