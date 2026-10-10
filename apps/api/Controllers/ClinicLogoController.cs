using System.Security.Cryptography;
using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ClinicaRamirez.Api.Controllers;

[ApiController, Tags("Clinic Logo")]
public sealed class ClinicLogoController(ClinicLogoService service) : ControllerBase
{
    [HttpPut("api/admin/clinic/logo"), RequirePermission("clinic.update")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024, MemoryBufferThreshold = 6 * 1024 * 1024)]
    public async Task<ActionResult<ClinicLogoResponse>> Upload(IFormFile file, CancellationToken ct)
        => Ok(await service.Upload(file, ct));

    [HttpGet("api/clinic/logo"), AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var logo = await service.Get(ct);
        Response.Headers.CacheControl = "public, max-age=0, must-revalidate";
        Response.Headers.XContentTypeOptions = "nosniff";
        var etag = new EntityTagHeaderValue('"' + Convert.ToHexString(SHA256.HashData(logo.Bytes)) + '"');
        return File(logo.Bytes, logo.ContentType, lastModified: logo.UpdatedAt, entityTag: etag);
    }
    [HttpDelete("api/admin/clinic/logo"), RequirePermission("clinic.update")]
    public async Task<IActionResult> Remove(CancellationToken ct) { await service.Remove(ct); return NoContent(); }
}
