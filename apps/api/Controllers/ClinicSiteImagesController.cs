using System.Security.Cryptography;
using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ClinicaRamirez.Api.Controllers;

[ApiController, Tags("Clinic Site Images")]
public sealed class ClinicSiteImagesController(ClinicSiteImageService service) : ControllerBase
{
    [HttpPut("api/admin/clinic/site-images/hero"), RequirePermission("clinic.update"), Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024, MemoryBufferThreshold = 11 * 1024 * 1024)]
    public async Task<ActionResult<HeroImageResponse>> Upload([FromForm] HeroUploadRequest request, CancellationToken ct) => Ok(await service.Upload(request, ct));
    [HttpGet("api/admin/clinic/site-images/hero"), RequirePermission("clinic.view")]
    public async Task<ActionResult<HeroImageResponse>> Admin(CancellationToken ct) => Ok(await service.GetAdmin(ct));
    [HttpDelete("api/admin/clinic/site-images/hero"), RequirePermission("clinic.update")]
    public async Task<IActionResult> Remove(CancellationToken ct) { await service.Remove(ct); return NoContent(); }
    [HttpGet("api/clinic/site-images/hero/meta"), AllowAnonymous]
    public async Task<ActionResult<HeroPublicMetadata>> Metadata(CancellationToken ct) {
        Response.Headers.CacheControl = "no-store"; return Ok(await service.GetMetadata(ct));
    }
    [HttpGet("api/clinic/site-images/hero"), AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct) {
        Response.Headers.CacheControl = "public, max-age=0, must-revalidate";
        var image = await service.Get(ct);
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Bytes, image.ContentType, lastModified: image.UpdatedAt,
            entityTag: new EntityTagHeaderValue('"' + Convert.ToHexString(SHA256.HashData(image.Bytes)) + '"'));
    }
}
