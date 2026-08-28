using Freight.Application.Simulation;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("simulation")]
public sealed class SimulationController(
    SimulationClockHandler simulationClockHandler,
    SimulationAdvanceHandler simulationAdvanceHandler) : ControllerBase
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
    public async Task<ActionResult<AdvanceSimulationResponse>> Advance(AdvanceSimulationBody body, CancellationToken cancellationToken)
    {
        var response = await simulationAdvanceHandler.HandleAsync(new AdvanceSimulationRequest(body.Ticks), cancellationToken);
        return Ok(response);
    }
}

public sealed record SetSimulationTimeBody(DateTime NewCurrentTime);

public sealed record AdvanceSimulationBody(int Ticks);
