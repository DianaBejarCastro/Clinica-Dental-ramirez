using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaRamirez.Api.Controllers;
[ApiController, Tags("Clinic Social Networks")]
public sealed class ClinicSocialNetworksController(ClinicSocialNetworkService service) : ControllerBase
{
    private const string Root = "api/admin/clinic/social-networks";
    [HttpGet(Root), RequirePermission("clinic.view")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.List(ct));
    [HttpGet(Root + "/{id:guid}"), RequirePermission("clinic.view")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await service.Get(id, ct));
    [HttpPost(Root), RequirePermission("clinic.update")]
    public async Task<IActionResult> Create(ClinicSocialNetworkCreateRequest request, CancellationToken ct)
    { var result = await service.Save(null, request, ct); return Created($"/{Root}/{result.Id}", result); }
    [HttpPut(Root + "/{id:guid}"), RequirePermission("clinic.update")]
    public async Task<IActionResult> Update(Guid id, ClinicSocialNetworkUpdateRequest request, CancellationToken ct) => Ok(await service.Save(id, request, ct));
    [HttpPatch(Root + "/{id:guid}/status"), RequirePermission("clinic.update")]
    public async Task<IActionResult> Status(Guid id, ClinicSocialNetworkStatusRequest request, CancellationToken ct) => Ok(await service.Status(id, request.IsActive, ct));
    [HttpPut(Root + "/{id:guid}/logo"), RequirePermission("clinic.update"), Consumes("multipart/form-data")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 3 * 1024 * 1024, MemoryBufferThreshold = 3 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, IFormFile file, CancellationToken ct) => Ok(await service.Upload(id, file, ct));
    [HttpDelete(Root + "/{id:guid}/logo"), RequirePermission("clinic.update")]
    public async Task<IActionResult> RemoveLogo(Guid id, CancellationToken ct) { await service.RemoveLogo(id, ct); return NoContent(); }
    [HttpGet("api/clinic/social-networks"), AllowAnonymous]
    public async Task<IActionResult> PublicList(CancellationToken ct) => Ok(await service.PublicList(ct));
    [HttpGet("api/clinic/social-networks/{id:guid}/logo"), AllowAnonymous]
    public async Task<IActionResult> Logo(Guid id, CancellationToken ct)
    {
        var logo = await service.Logo(id, ct);
        Response.Headers.CacheControl = "public, max-age=0, must-revalidate"; Response.Headers.XContentTypeOptions = "nosniff";
        return File(logo.Bytes, logo.ContentType);
    }
}
