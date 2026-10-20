using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaRamirez.Api.Controllers;
[ApiController, Route("api/location/search"), RequirePermission("centers.view")]
public sealed class LocationSearchController(LocationSearchService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<LocationSearchResult[]>> Search([FromQuery] string? q, CancellationToken ct)
        => Ok(await service.Search(q, ct));
}
