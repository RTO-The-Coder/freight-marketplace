using Freight.Application.Fleet;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects.RuleVariants;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
public sealed class DriversController(
    AddDriverHandler addDriverHandler,
    GetDriversHandler getDriversHandler,
    GetTruckForDriverHandler getTruckForDriverHandler,
    GetDriverDetailHandler getDriverDetailHandler,
    CheckDriverEligibilityHandler checkDriverEligibilityHandler) : ControllerBase
{
    [HttpPost("drivers")]
    public async Task<ActionResult<AddDriverResponse>> AddDriver(AddDriverBody body, CancellationToken cancellationToken)
    {
        var response = await addDriverHandler.HandleAsync(
            new AddDriverRequest(
                body.FirstName,
                body.LastName,
                body.BreakRule,
                body.DailyRestRule,
                body.WeeklyRestRule,
                body.ExtendDailyDrivingWhenEligible),
            cancellationToken);
        return Ok(response);
    }

    [HttpGet("drivers")]
    public async Task<ActionResult<GetDriversResponse>> GetDrivers([FromQuery] bool unassigned, CancellationToken cancellationToken)
    {
        var response = await getDriversHandler.HandleAsync(new GetDriversRequest(unassigned), cancellationToken);
        return Ok(response);
    }

    [HttpGet("drivers/{driverId:guid}/truck")]
    public async Task<ActionResult<GetTruckForDriverResponse>> GetTruckForDriver(Guid driverId, CancellationToken cancellationToken)
    {
        var response = await getTruckForDriverHandler.HandleAsync(new GetTruckForDriverRequest(driverId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("drivers/{driverId:guid}")]
    public async Task<ActionResult<DriverDetailDto>> GetDriverDetail(Guid driverId, CancellationToken cancellationToken)
    {
        var response = await getDriverDetailHandler.HandleAsync(new GetDriverDetailRequest(driverId), cancellationToken);
        return Ok(response);
    }

    [HttpPost("drivers/{driverId:guid}/eligibility-check")]
    public async Task<ActionResult<CheckDriverEligibilityResponse>> CheckDriverEligibility(
        Guid driverId,
        CheckDriverEligibilityBody body,
        CancellationToken cancellationToken)
    {
        var response = await checkDriverEligibilityHandler.HandleAsync(
            new CheckDriverEligibilityRequest(driverId, body.AfterMinutes),
            cancellationToken);
        return Ok(response);
    }
}

public sealed record AssignDriversBody(Guid PrimaryDriverId, Guid? SecondaryDriverId);

public sealed record AddDriverBody(
    string FirstName,
    string LastName,
    DrivingBreakRule BreakRule,
    DailyRestRule DailyRestRule,
    WeeklyRestRule WeeklyRestRule,
    bool ExtendDailyDrivingWhenEligible);

public sealed record CheckDriverEligibilityBody(int AfterMinutes);
