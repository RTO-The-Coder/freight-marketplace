using Freight.Application.Geocoding;
using Microsoft.AspNetCore.Mvc;

namespace Freight.Api.Controllers;

[ApiController]
public sealed class GeocodingController(SearchAreasHandler searchAreasHandler) : ControllerBase
{
    /// <summary>
    /// The areas matching a postcode and suburb, from Nominatim - up to five, each with its
    /// country. Returns 503 when Nominatim is unreachable or errors.
    /// </summary>
    [HttpGet("geocoding/search")]
    public async Task<ActionResult<SearchAreasResponse>> Search(
        [FromQuery] string postcode,
        [FromQuery] string suburb,
        CancellationToken cancellationToken)
    {
        var response = await searchAreasHandler.SearchAreasAsync(new SearchAreasRequest(postcode, suburb), cancellationToken);
        return Ok(response);
    }
}
