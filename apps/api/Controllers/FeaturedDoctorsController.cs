using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController, Tags("Home Team")]
public sealed class FeaturedDoctorsController(FeaturedDoctorsService service) : ControllerBase
{
    private const string Root = "api/admin/clinic/team";
    [HttpGet(Root), RequirePermission("clinic.view")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.List(ct));
    [HttpGet(Root + "/available"), RequirePermission("clinic.view")]
    public async Task<IActionResult> Available(CancellationToken ct) => Ok(await service.Available(ct));
    [HttpPost(Root), RequirePermission("clinic.update")]
    public async Task<IActionResult> Add(FeaturedDoctorRequest request, CancellationToken ct)
    { var id = await service.Add(request, ct); return Created($"/{Root}/{id}", new { id }); }
    [HttpPut(Root + "/{id:guid}/order"), RequirePermission("clinic.update")]
    public async Task<IActionResult> Order(Guid id, FeaturedDoctorOrderRequest request, CancellationToken ct)
    { await service.Change(id, request.DisplayOrder, ct); return NoContent(); }
    [HttpDelete(Root + "/{id:guid}"), RequirePermission("clinic.update")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    { await service.Change(id, null, ct); return NoContent(); }
    [HttpGet("api/clinic/team"), AllowAnonymous]
    public async Task<IActionResult> Public(CancellationToken ct) => Ok(await service.Public(ct));
    [HttpGet("api/clinic/team/{id:guid}/photo"), AllowAnonymous]
    public async Task<IActionResult> Photo(Guid id, CancellationToken ct)
    {
        var photo = await service.Photo(id, ct);
        Response.Headers.CacheControl = "public, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        var tag = $"\"{photo.Version.Ticks}\""; Response.Headers.ETag = tag;
        if (Request.Headers.IfNoneMatch.Any(x => x == tag)) return StatusCode(304);
        return File(photo.Bytes, photo.MimeType);
    }
}
