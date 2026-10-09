using Freight.Application.Evaluation;
using Freight.Application.Fleet;
using Freight.Application.Offers;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
[Route("companies")]
public sealed class TruckingCompaniesController(
    GetTruckingCompaniesHandler getTruckingCompaniesHandler,
    GetTruckingCompanyByIdHandler getTruckingCompanyByIdHandler,
    GetFleetTreeHandler getFleetTreeHandler,
    EvaluateShipmentForCompanyHandler evaluateShipmentForCompanyHandler,
    RegisterDeviceTokenHandler registerDeviceTokenHandler,
    UnregisterDeviceTokenHandler unregisterDeviceTokenHandler,
    GetCompanyShipmentBoardHandler getCompanyShipmentBoardHandler,
    SendOffersHandler sendOffersHandler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<GetTruckingCompaniesResponse>> GetTruckingCompanies(CancellationToken cancellationToken)
    {
        var response = await getTruckingCompaniesHandler.GetTruckingCompaniesAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("{companyId:guid}")]
    public async Task<ActionResult<TruckingCompanySummaryDto>> GetTruckingCompany(Guid companyId, CancellationToken cancellationToken)
    {
        var response = await getTruckingCompanyByIdHandler.GetTruckingCompanyByIdAsync(
            new GetTruckingCompanyByIdRequest(companyId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{companyId:guid}/fleet")]
    public async Task<ActionResult<GetFleetTreeResponse>> GetFleetTree(Guid companyId, CancellationToken cancellationToken)
    {
        var response = await getFleetTreeHandler.HandleAsync(new GetFleetTreeRequest(companyId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{companyId:guid}/shipments/{shipmentId:guid}/evaluate")]
    public async Task<ActionResult<EvaluateShipmentForCompanyResponse>> EvaluateShipment(
        Guid companyId, Guid shipmentId, CancellationToken cancellationToken)
    {
        var response = await evaluateShipmentForCompanyHandler.EvaluateAsync(
            new EvaluateShipmentForCompanyRequest(shipmentId, companyId), cancellationToken);
        return Ok(response);
    }

    [HttpGet("{companyId:guid}/shipments/board")]
    public async Task<ActionResult<GetCompanyShipmentBoardResponse>> GetShipmentBoard(
        Guid companyId, CancellationToken cancellationToken)
    {
        var response = await getCompanyShipmentBoardHandler.GetBoardAsync(
            new GetCompanyShipmentBoardRequest(companyId), cancellationToken);
        return Ok(response);
    }

    public sealed record OfferItemBody(Guid TruckId, decimal PriceEur, DateTime? LimitAt);

    public sealed record SendOffersBody(IReadOnlyList<OfferItemBody> Offers);

    [HttpPost("{companyId:guid}/shipments/{shipmentId:guid}/offers")]
    public async Task<ActionResult<SendOffersResponse>> SendOffers(
        Guid companyId, Guid shipmentId, SendOffersBody body, CancellationToken cancellationToken)
    {
        var response = await sendOffersHandler.SendOffersAsync(
            new SendOffersRequest(
                companyId,
                shipmentId,
                body.Offers.Select(offer => new OfferItem(offer.TruckId, offer.PriceEur, offer.LimitAt)).ToList()),
            cancellationToken);
        return Ok(response);
    }

    public sealed record DeviceTokenBody(string Fid);

    [HttpPost("{companyId:guid}/device-token")]
    public async Task<IActionResult> RegisterDeviceToken(
        Guid companyId, DeviceTokenBody body, CancellationToken cancellationToken)
    {
        await registerDeviceTokenHandler.RegisterAsync(
            new RegisterDeviceTokenRequest(companyId, body.Fid), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{companyId:guid}/device-token")]
    public async Task<IActionResult> UnregisterDeviceToken(
        Guid companyId, DeviceTokenBody body, CancellationToken cancellationToken)
    {
        await unregisterDeviceTokenHandler.UnregisterAsync(
            new UnregisterDeviceTokenRequest(companyId, body.Fid), cancellationToken);
        return NoContent();
    }
}
