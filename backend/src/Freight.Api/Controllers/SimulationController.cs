using Freight.Application.Simulation;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("simulation")]
public sealed class SimulationController(SimulationClockHandler simulationClockHandler) : ControllerBase
{
    [HttpGet("time")]
    public async Task<ActionResult<SimulationTimeResponse>> GetTime(CancellationToken cancellationToken)
    {
        var response = await simulationClockHandler.GetTimeAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost("time")]
    public async Task<ActionResult<SimulationTimeResponse>> SetTime(SetSimulationTimeBody body, CancellationToken cancellationToken)
    {
        var response = await simulationClockHandler.SetTimeAsync(new SetSimulationTimeRequest(body.NewCurrentTime), cancellationToken);
        return Ok(response);
    }

    [HttpPost("advance")]
    public async Task<ActionResult<SimulationTimeResponse>> Advance(AdvanceSimulationTimeBody body, CancellationToken cancellationToken)
    {
        var response = await simulationClockHandler.AdvanceAsync(new AdvanceSimulationTimeRequest(body.Minutes), cancellationToken);
        return Ok(response);
    }
}

public sealed record SetSimulationTimeBody(DateTime NewCurrentTime);

public sealed record AdvanceSimulationTimeBody(int Minutes);
