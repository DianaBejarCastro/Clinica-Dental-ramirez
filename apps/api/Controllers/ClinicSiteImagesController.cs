using System.Security.Cryptography;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Application.Identity;
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
    private static ClinicSiteImageType Purpose(string purpose) => purpose switch {
        "hero" => ClinicSiteImageType.HERO,
        "care" => ClinicSiteImageType.CARE,
        _ => throw new ApiException(404, "Tipo de imagen inexistente.")
    };
    [HttpPut("api/admin/clinic/site-images/{purpose}"), RequirePermission("clinic.update"), Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024, MemoryBufferThreshold = 11 * 1024 * 1024)]
    public async Task<ActionResult<HeroImageResponse>> Upload([FromForm] HeroUploadRequest request, CancellationToken ct, string purpose) => Ok(await service.Upload(request, ct, Purpose(purpose)));
    [HttpGet("api/admin/clinic/site-images/{purpose}"), RequirePermission("clinic.view")]
    public async Task<ActionResult<HeroImageResponse>> Admin(CancellationToken ct, string purpose) => Ok(await service.GetAdmin(ct, Purpose(purpose)));
    [HttpDelete("api/admin/clinic/site-images/{purpose}"), RequirePermission("clinic.update")]
    public async Task<IActionResult> Remove(CancellationToken ct, string purpose) { await service.Remove(ct, Purpose(purpose)); return NoContent(); }
    [HttpGet("api/clinic/site-images/{purpose}/meta"), AllowAnonymous]
    public async Task<ActionResult<HeroPublicMetadata>> Metadata(CancellationToken ct, string purpose) {
        Response.Headers.CacheControl = "no-store"; return Ok(await service.GetMetadata(ct, Purpose(purpose)));
    }
    [HttpGet("api/clinic/site-images/{purpose}"), AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct, string purpose) {
        Response.Headers.CacheControl = "public, max-age=0, must-revalidate";
        var image = await service.Get(ct, Purpose(purpose));
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(image.Bytes, image.ContentType, lastModified: image.UpdatedAt,
            entityTag: new EntityTagHeaderValue('"' + Convert.ToHexString(SHA256.HashData(image.Bytes)) + '"'));
    }
}
