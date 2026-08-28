using Freight.Application.Fleet;
using Freight.Domain.Fleet;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
public sealed class TruckController(
    AddTruckHandler addTruckHandler,
    SetTruckActivationHandler truckActivationHandler,
    GetTrucksHandler getTrucksHandler,
    GetTruckDetailHandler getTruckDetailHandler,
    SetTruckCompanyHandler assignTruckToCompanyHandler,
    AssignDriversHandler assignDriversHandler,
    AssignShipmentToTruckHandler assignShipmentToTruckHandler) : ControllerBase
{
    [HttpPost("trucks")]
    public async Task<ActionResult<AddTruckResponse>> AddTruck(AddTruckBody body, CancellationToken cancellationToken)
    {
        var response = await addTruckHandler.AddTruckAsync(
            new AddTruckRequest(body.TruckName, body.TruckType, body.TruckSize),
            cancellationToken);
        return CreatedAtAction(nameof(GetTruckDetail), new { truckId = response.TruckId }, response);
    }

    [HttpPost("trucks/{truckId:guid}/company")]
    public async Task<IActionResult> AssignTruckToCompany(Guid truckId, AssignTruckToCompanyBody body, CancellationToken cancellationToken)
    {
        await assignTruckToCompanyHandler.AssignmentTruckingCompany(new SetTruckCompanyRequest(truckId, body.TruckingCompanyId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("trucks/{truckId:guid}/company")]
    public async Task<IActionResult> UnassignTruckFromCompany(Guid truckId, CancellationToken cancellationToken)
    {
        await assignTruckToCompanyHandler.AssignmentTruckingCompany(new SetTruckCompanyRequest(truckId, null), cancellationToken);
        return NoContent();
    }

    [HttpPost("trucks/{truckId:guid}/activate")]
    public async Task<IActionResult> ActivateTruck(Guid truckId, CancellationToken cancellationToken)
    {
        await truckActivationHandler.HandleActivation(new SetTruckActivationRequest(truckId, true), cancellationToken);
        return NoContent();
    }

    [HttpPost("trucks/{truckId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateTruck(Guid truckId, CancellationToken cancellationToken)
    {
        await truckActivationHandler.HandleActivation(new SetTruckActivationRequest(truckId, false), cancellationToken);
        return NoContent();
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

    [HttpGet("trucks/{truckId:guid}")]
    public async Task<ActionResult<TruckDetailDto>> GetTruckDetail(Guid truckId, CancellationToken cancellationToken)
    {
        var response = await getTruckDetailHandler.HandleAsync(new GetTruckDetailRequest(truckId), cancellationToken);
        return Ok(response);
    }

    [HttpPost("trucks/{truckId:guid}/assign-shipment")]
    public async Task<ActionResult<AssignShipmentToTruckResponse>> AssignShipmentToTruck(
        Guid truckId,
        AssignShipmentToTruckBody body,
        CancellationToken cancellationToken)
    {
        var response = await assignShipmentToTruckHandler.AssignShipment(
            new AssignShipmentToTruckRequest(
                truckId, body.ShipmentId, body.PickupInsertIndex, body.DeliveryInsertIndex, body.TripStartTime),
            cancellationToken);
        return Ok(response);
    }

    [HttpPatch("trucks/{truckId:guid}/drivers")]
    public async Task<IActionResult> AssignDrivers(Guid truckId, AssignDriversBody body, CancellationToken cancellationToken)
    {
        await assignDriversHandler.AssignDrivers(
            new AssignDriversRequest(truckId, body.PrimaryDriverId, body.SecondaryDriverId),
            cancellationToken);
        return NoContent();
    }
}

public sealed record AssignTruckToCompanyBody(Guid TruckingCompanyId);

public sealed record AddTruckBody(string TruckName, TruckType TruckType, TruckSize TruckSize);

public sealed record AssignShipmentToTruckBody(
    Guid ShipmentId,
    int PickupInsertIndex,
    int DeliveryInsertIndex,
    DateTime? TripStartTime = null);