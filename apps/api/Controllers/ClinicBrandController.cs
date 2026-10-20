using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Controllers;

public sealed record ClinicBrandResponse(string Name);

[ApiController, Route("api/clinic/brand"), AllowAnonymous]
public sealed class ClinicBrandController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ClinicBrandResponse>> Get(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-cache";
        var brand = await db.Clinics.AsNoTracking().Select(x => new ClinicBrandResponse(x.Name)).SingleOrDefaultAsync(ct);
        return brand is null ? NotFound() : Ok(brand);
    }
}
