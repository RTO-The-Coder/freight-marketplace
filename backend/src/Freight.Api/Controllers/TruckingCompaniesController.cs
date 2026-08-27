using Freight.Application.Fleet;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("companies")]
public sealed class TruckingCompaniesController(GetTruckingCompaniesHandler getTruckingCompaniesHandler, GetFleetTreeHandler getFleetTreeHandler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GetTruckingCompaniesResponse>> GetTruckingCompanies(CancellationToken cancellationToken)
    {
        var response = await getTruckingCompaniesHandler.HandleAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("{companyId:guid}/fleet")]
    public async Task<ActionResult<GetFleetTreeResponse>> GetFleetTree(Guid companyId, CancellationToken cancellationToken)
    {
        var response = await getFleetTreeHandler.HandleAsync(new GetFleetTreeRequest(companyId), cancellationToken);
        return Ok(response);
    }
}
