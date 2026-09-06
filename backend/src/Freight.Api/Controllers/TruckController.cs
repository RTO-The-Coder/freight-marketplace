using Freight.Application.Fleet;
using Freight.Domain.Fleet.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
public sealed class TruckController(
    AddTruckHandler addTruckHandler,
    SetTruckActivationHandler truckActivationHandler,
    GetTrucksHandler getTrucksHandler,
    GetTruckDetailHandler getTruckDetailHandler,
    GetTruckEtasHandler getTruckEtasHandler,
    GetTruckPositionHandler getTruckPositionHandler,
    SetTruckCompanyHandler assignTruckToCompanyHandler,
    AssignDriversHandler assignDriversHandler,
    RemoveDriversHandler removeDriversHandler,
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
        await assignTruckToCompanyHandler.SetTruckCompanyAsync(new SetTruckCompanyRequest(truckId, body.TruckingCompanyId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("trucks/{truckId:guid}/company")]
    public async Task<IActionResult> UnassignTruckFromCompany(Guid truckId, CancellationToken cancellationToken)
    {
        await assignTruckToCompanyHandler.SetTruckCompanyAsync(new SetTruckCompanyRequest(truckId, null), cancellationToken);
        return NoContent();
    }

    [HttpPost("trucks/{truckId:guid}/activate")]
    public async Task<IActionResult> ActivateTruck(Guid truckId, CancellationToken cancellationToken)
    {
        await truckActivationHandler.SetTruckActivationAsync(new SetTruckActivationRequest(truckId, true), cancellationToken);
        return NoContent();
    }

    [HttpPost("trucks/{truckId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateTruck(Guid truckId, CancellationToken cancellationToken)
    {
        await truckActivationHandler.SetTruckActivationAsync(new SetTruckActivationRequest(truckId, false), cancellationToken);
        return NoContent();
    }

    [HttpGet("trucks")]
    public async Task<ActionResult<GetTrucksResponse>> GetTrucks(
            [FromQuery] bool unassigned,
            [FromQuery] Guid? truckingCompanyId,
            CancellationToken cancellationToken)
    {
        var response = await getTrucksHandler.GetTrucksAsync(new GetTrucksRequest(unassigned, truckingCompanyId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("trucks/{truckId:guid}")]
    public async Task<ActionResult<TruckDetailDto>> GetTruckDetail(Guid truckId, CancellationToken cancellationToken)
    {
        var response = await getTruckDetailHandler.GetTruckDetailAsync(new GetTruckDetailRequest(truckId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("trucks/{truckId:guid}/etas")]
    public async Task<ActionResult<TruckEtasDto>> GetTruckEtas(Guid truckId, CancellationToken cancellationToken)
    {
        var response = await getTruckEtasHandler.GetTruckEtasAsync(new GetTruckEtasRequest(truckId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("trucks/{truckId:guid}/position")]
    public async Task<ActionResult<TruckPositionDto>> GetTruckPosition(Guid truckId, CancellationToken cancellationToken)
    {
        var response = await getTruckPositionHandler.GetTruckPositionAsync(new GetTruckPositionRequest(truckId), cancellationToken);
        return Ok(response);
    }

    [HttpPost("trucks/{truckId:guid}/assign-shipment")]
    public async Task<ActionResult<AssignShipmentToTruckResponse>> AssignShipmentToTruck(
        Guid truckId,
        AssignShipmentToTruckBody body,
        CancellationToken cancellationToken)
    {
        var response = await assignShipmentToTruckHandler.AssignShipmentAsync(
            new AssignShipmentToTruckRequest(
                truckId, body.ShipmentId, body.PickupInsertIndex, body.DeliveryInsertIndex, body.TripStartTime),
            cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Dry run of <see cref="AssignShipmentToTruck"/>: reports whether the shipment could
    /// be inserted at the given positions (route/window/capacity all check out) without
    /// committing anything. A 4xx here still means a hard precondition failed (unknown
    /// truck, inactive truck, type mismatch); a 200 with <c>isFeasible: false</c> means
    /// the insertion itself is not viable, with a reason.
    /// </summary>
    [HttpPost("trucks/{truckId:guid}/assign-shipment/feasibility")]
    public async Task<ActionResult<ShipmentFeasibilityResponse>> CheckAssignShipmentFeasibility(
        Guid truckId,
        AssignShipmentToTruckBody body,
        CancellationToken cancellationToken)
    {
        var response = await assignShipmentToTruckHandler.CheckFeasibilityAsync(
            new AssignShipmentToTruckRequest(
                truckId, body.ShipmentId, body.PickupInsertIndex, body.DeliveryInsertIndex, body.TripStartTime),
            cancellationToken);
        return Ok(response);
    }

    [HttpPatch("trucks/{truckId:guid}/drivers")]
    public async Task<IActionResult> AssignDrivers(Guid truckId, AssignDriversBody body, CancellationToken cancellationToken)
    {
        await assignDriversHandler.AssignDriversAsync(
            new AssignDriversRequest(truckId, body.PrimaryDriverId, body.SecondaryDriverId),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("trucks/{truckId:guid}/drivers")]
    public async Task<IActionResult> RemoveDrivers(Guid truckId, CancellationToken cancellationToken)
    {
        await removeDriversHandler.RemoveDriversAsync(new RemoveDriversRequest(truckId), cancellationToken);
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