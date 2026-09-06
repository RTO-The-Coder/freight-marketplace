using Freight.Application.Fleet;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("companies")]
public sealed class TruckingCompaniesController(
    GetTruckingCompaniesHandler getTruckingCompaniesHandler,
    GetTruckingCompanyByIdHandler getTruckingCompanyByIdHandler,
    GetFleetTreeHandler getFleetTreeHandler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GetTruckingCompaniesResponse>> GetTruckingCompanies(CancellationToken cancellationToken)
    {
        var response = await getTruckingCompaniesHandler.GetTruckingCompaniesAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("{companyId:guid}")]
    public async Task<ActionResult<TruckingCompanySummaryDto>> GetTruckingCompany(Guid companyId, CancellationToken cancellationToken)
    {
        var response = await getTruckingCompanyByIdHandler.GetTruckingCompanyByIdAsync(
            new GetTruckingCompanyByIdRequest(companyId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{companyId:guid}/fleet")]
    public async Task<ActionResult<GetFleetTreeResponse>> GetFleetTree(Guid companyId, CancellationToken cancellationToken)
    {
        var response = await getFleetTreeHandler.HandleAsync(new GetFleetTreeRequest(companyId), cancellationToken);
        return Ok(response);
    }
}
