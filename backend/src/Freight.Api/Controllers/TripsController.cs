using Freight.Application.Fleet;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("trips")]
public sealed class TripsController(RescheduleTripHandler rescheduleTripHandler) : ControllerBase
{
    /// <summary>
    /// Changes a not-yet-moved trip's planned departure. 400 if the trip has completed,
    /// reached a stop, or its truck has started driving.
    /// </summary>
    [HttpPatch("{tripId:guid}/start")]
    public async Task<ActionResult<RescheduleTripResponse>> RescheduleStart(
        Guid tripId, RescheduleTripBody body, CancellationToken cancellationToken)
    {
        var response = await rescheduleTripHandler.RescheduleTripAsync(
            new RescheduleTripRequest(tripId, body.NewStartTime), cancellationToken);
        return Ok(response);
    }
}

public sealed record RescheduleTripBody(DateTime NewStartTime);
