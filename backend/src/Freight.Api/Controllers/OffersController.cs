using Freight.Application.Fleet;
using Freight.Application.Offers;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("offers")]
public sealed class OffersController(
    AcceptOfferHandler acceptOfferHandler,
    AddOfferToTripHandler addOfferToTripHandler) : ControllerBase
{
    [HttpPost("{offerId:guid}/accept")]
    public async Task<ActionResult<AcceptOfferResponse>> AcceptOffer(Guid offerId, CancellationToken cancellationToken)
    {
        var response = await acceptOfferHandler.AcceptOfferAsync(new AcceptOfferRequest(offerId), cancellationToken);
        return Ok(response);
    }

    [HttpPost("{offerId:guid}/add-to-trip")]
    public async Task<ActionResult<AssignShipmentToTruckResponse>> AddToTrip(Guid offerId, CancellationToken cancellationToken)
    {
        var response = await addOfferToTripHandler.AddToTripAsync(new AddOfferToTripRequest(offerId), cancellationToken);
        return Ok(response);
    }
}
