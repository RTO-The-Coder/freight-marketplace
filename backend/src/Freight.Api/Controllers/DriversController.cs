using Freight.Application.Fleet;
using Freight.Application.Tracking;
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
    CheckDriverEligibilityHandler checkDriverEligibilityHandler,
    SetDriverCompanyHandler setDriverCompanyHandler) : ControllerBase
{
    [HttpPost("drivers")]
    public async Task<ActionResult<AddDriverResponse>> AddDriver(AddDriverBody body, CancellationToken cancellationToken)
    {
        var response = await addDriverHandler.AddDriverAsync(
            new AddDriverRequest(
                body.FirstName,
                body.LastName,
                body.BreakRule,
                body.DailyRestRule,
                body.WeeklyRestRule,
                body.ExtendDailyDrivingWhenEligible,
                body.TruckingCompanyId),
            cancellationToken);
        return CreatedAtAction(nameof(GetDriverDetail), new { driverId = response.DriverId }, response);
    }

    [HttpGet("drivers")]
    public async Task<ActionResult<GetDriversResponse>> GetDrivers(
        [FromQuery] bool unassigned,
        [FromQuery] Guid? truckingCompanyId,
        CancellationToken cancellationToken)
    {
        var response = await getDriversHandler.GetDriversAsync(new GetDriversRequest(unassigned, truckingCompanyId), cancellationToken);
        return Ok(response);
    }

    [HttpPost("drivers/{driverId:guid}/company")]
    public async Task<IActionResult> AssignDriverToCompany(Guid driverId, AssignDriverToCompanyBody body, CancellationToken cancellationToken)
    {
        await setDriverCompanyHandler.SetDriverCompanyAsync(new SetDriverCompanyRequest(driverId, body.TruckingCompanyId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("drivers/{driverId:guid}/company")]
    public async Task<IActionResult> UnassignDriverFromCompany(Guid driverId, CancellationToken cancellationToken)
    {
        await setDriverCompanyHandler.SetDriverCompanyAsync(new SetDriverCompanyRequest(driverId, null), cancellationToken);
        return NoContent();
    }

    [HttpGet("drivers/{driverId:guid}/truck")]
    public async Task<ActionResult<GetTruckForDriverResponse>> GetTruckForDriver(Guid driverId, CancellationToken cancellationToken)
    {
        var response = await getTruckForDriverHandler.GetTruckForDriverAsync(new GetTruckForDriverRequest(driverId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("drivers/{driverId:guid}")]
    public async Task<ActionResult<DriverDetailDto>> GetDriverDetail(Guid driverId, CancellationToken cancellationToken)
    {
        var response = await getDriverDetailHandler.GetDriverDetailAsync(new GetDriverDetailRequest(driverId), cancellationToken);
        return Ok(response);
    }

    [HttpPost("drivers/{driverId:guid}/eligibility-check")]
    public async Task<ActionResult<CheckDriverEligibilityResponse>> CheckDriverEligibility(
        Guid driverId,
        CheckDriverEligibilityBody body,
        CancellationToken cancellationToken)
    {
        var response = await checkDriverEligibilityHandler.CheckDriverEligibilityAsync(
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
    bool ExtendDailyDrivingWhenEligible,
    Guid TruckingCompanyId);

public sealed record AssignDriverToCompanyBody(Guid TruckingCompanyId);

public sealed record CheckDriverEligibilityBody(int AfterMinutes);
