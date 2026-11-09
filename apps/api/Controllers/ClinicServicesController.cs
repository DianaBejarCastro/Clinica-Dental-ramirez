using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Infrastructure.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Controllers;
[ApiController, Tags("Home Services")]
public sealed class ClinicServicesController(HomeServicesService service, AppDbContext db) : ControllerBase
{
    private const string Root = "api/admin/clinic/services";
    [HttpGet(Root), RequirePermission("clinic.view")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.List(ct));
    [HttpGet(Root + "/specialties"), RequirePermission("clinic.view")]
    public async Task<IActionResult> Specialties(CancellationToken ct) => Ok(await db.Specialties.AsNoTracking()
        .OrderBy(x => x.Name).Select(x => new SpecialtyOption(x.Id, x.Name)).ToArrayAsync(ct));
    [HttpPost(Root), RequirePermission("clinic.update")]
    public async Task<IActionResult> Create(ClinicServiceRequest request, CancellationToken ct)
    { var result = await service.Save(null, request, ct); return Created($"/{Root}/{result.Id}", result); }
    [HttpPut(Root + "/{id:guid}"), RequirePermission("clinic.update")]
    public async Task<IActionResult> Update(Guid id, ClinicServiceRequest request, CancellationToken ct) => Ok(await service.Save(id, request, ct));
    [HttpPatch(Root + "/{id:guid}/status"), RequirePermission("clinic.update")]
    public async Task<IActionResult> Status(Guid id, ClinicServiceStatusRequest request, CancellationToken ct)
    { await service.Status(id, request.IsActive, ct); return NoContent(); }
    [HttpGet("api/clinic/services"), AllowAnonymous]
    public async Task<IActionResult> Public(CancellationToken ct) => Ok(await service.PublicList(ct));
    public sealed record SpecialtyOption(Guid Id, string Name);
}
