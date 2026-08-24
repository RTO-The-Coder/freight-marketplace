using Freight.Application.Fleet;
using Freight.Domain.Fleet;
using Freight.Domain.ValueObjects.RuleVariants;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
public sealed class FleetController(
    AddTruckHandler addTruckHandler,
    AddDriverHandler addDriverHandler,
    AssignDriversHandler assignDriversHandler,
    ActivateTruckHandler activateTruckHandler,
    DeactivateTruckHandler deactivateTruckHandler,
    GetFleetTreeHandler getFleetTreeHandler,
    GetTrucksHandler getTrucksHandler,
    GetDriversHandler getDriversHandler,
    GetTruckForDriverHandler getTruckForDriverHandler,
    GetTruckDetailHandler getTruckDetailHandler,
    GetDriverDetailHandler getDriverDetailHandler,
    AssignTruckToCompanyHandler assignTruckToCompanyHandler,
    UnassignTruckFromCompanyHandler unassignTruckFromCompanyHandler,
    AssignShipmentToTruckHandler assignShipmentToTruckHandler,
    CheckDriverEligibilityHandler checkDriverEligibilityHandler) : ControllerBase
{
    [HttpPost("trucks")]
    public async Task<ActionResult<AddTruckResponse>> AddTruck(AddTruckBody body, CancellationToken cancellationToken)
    {
        var response = await addTruckHandler.HandleAsync(
            new AddTruckRequest(body.TruckName, body.TruckType, body.TruckSize),
            cancellationToken);
        return Ok(response);
    }

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

    [HttpPatch("trucks/{truckId:guid}/drivers")]
    public async Task<IActionResult> AssignDrivers(
        Guid truckId,
        AssignDriversBody body,
        CancellationToken cancellationToken)
    {
        await assignDriversHandler.HandleAsync(
            new AssignDriversRequest(truckId, body.PrimaryDriverId, body.SecondaryDriverId),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("trucks/{truckId:guid}/activate")]
    public async Task<IActionResult> ActivateTruck(Guid truckId, CancellationToken cancellationToken)
    {
        await activateTruckHandler.HandleAsync(new ActivateTruckRequest(truckId), cancellationToken);
        return NoContent();
    }

    [HttpPost("trucks/{truckId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateTruck(Guid truckId, CancellationToken cancellationToken)
    {
        await deactivateTruckHandler.HandleAsync(new DeactivateTruckRequest(truckId), cancellationToken);
        return NoContent();
    }

    [HttpPost("trucks/{truckId:guid}/company")]
    public async Task<IActionResult> AssignTruckToCompany(Guid truckId, AssignTruckToCompanyBody body, CancellationToken cancellationToken)
    {
        await assignTruckToCompanyHandler.HandleAsync(new AssignTruckToCompanyRequest(truckId, body.TruckingCompanyId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("trucks/{truckId:guid}/company")]
    public async Task<IActionResult> UnassignTruckFromCompany(Guid truckId, CancellationToken cancellationToken)
    {
        await unassignTruckFromCompanyHandler.HandleAsync(new UnassignTruckFromCompanyRequest(truckId), cancellationToken);
        return NoContent();
    }

    [HttpGet("companies/{companyId:guid}/fleet")]
    public async Task<ActionResult<GetFleetTreeResponse>> GetFleetTree(Guid companyId, CancellationToken cancellationToken)
    {
        var response = await getFleetTreeHandler.HandleAsync(new GetFleetTreeRequest(companyId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("trucks")]
    public async Task<ActionResult<GetTrucksResponse>> GetTrucks(
        [FromQuery] bool unassigned,
        [FromQuery] Guid? truckingCompanyId,
        CancellationToken cancellationToken)
    {
        var response = await getTrucksHandler.HandleAsync(new GetTrucksRequest(unassigned, truckingCompanyId), cancellationToken);
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

    [HttpGet("trucks/{truckId:guid}")]
    public async Task<ActionResult<TruckDetailDto>> GetTruckDetail(Guid truckId, CancellationToken cancellationToken)
    {
        var response = await getTruckDetailHandler.HandleAsync(new GetTruckDetailRequest(truckId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("drivers/{driverId:guid}")]
    public async Task<ActionResult<DriverDetailDto>> GetDriverDetail(Guid driverId, CancellationToken cancellationToken)
    {
        var response = await getDriverDetailHandler.HandleAsync(new GetDriverDetailRequest(driverId), cancellationToken);
        return Ok(response);
    }

    [HttpPost("trucks/{truckId:guid}/assign-shipment")]
    public async Task<ActionResult<AssignShipmentToTruckResponse>> AssignShipmentToTruck(
        Guid truckId,
        AssignShipmentToTruckBody body,
        CancellationToken cancellationToken)
    {
        var response = await assignShipmentToTruckHandler.HandleAsync(
            new AssignShipmentToTruckRequest(truckId, body.ShipmentId),
            cancellationToken);
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

public sealed record AssignTruckToCompanyBody(Guid TruckingCompanyId);

public sealed record AssignDriversBody(Guid PrimaryDriverId, Guid? SecondaryDriverId);

public sealed record AddTruckBody(string TruckName, TruckType TruckType, TruckSize TruckSize);

public sealed record AddDriverBody(
    string FirstName,
    string LastName,
    DrivingBreakRule BreakRule,
    DailyRestRule DailyRestRule,
    WeeklyRestRule WeeklyRestRule,
    bool ExtendDailyDrivingWhenEligible);

public sealed record AssignShipmentToTruckBody(Guid ShipmentId);

public sealed record CheckDriverEligibilityBody(int AfterMinutes);
