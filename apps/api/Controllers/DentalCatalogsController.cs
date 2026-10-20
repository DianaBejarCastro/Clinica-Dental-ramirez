using ClinicaRamirez.Api.Application.DentalCharting;
using ClinicaRamirez.Api.Domain.DentalCharting;
using ClinicaRamirez.Api.DTOs.DentalCharting;
using ClinicaRamirez.Api.Infrastructure.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Controllers;

[ApiController,Authorize,Route("api/doctor/dental-catalogs"),RequirePermission("clinical_history.view")]
public sealed class DentalCatalogsController(AppDbContext db,ChartingAccess access):ControllerBase
{
    // Include inactive codes for exact historical interpretation; services reject new inactive selections.
    [HttpGet("teeth")] public async Task<DentalToothDto[]> Teeth() {await access.Doctor();return await db.Set<DentalTooth>().OrderBy(x=>x.DisplayOrder).Select(x=>new DentalToothDto(x.Id,x.FdiCode,x.DentitionClass,x.Quadrant,x.ToothType,x.DisplayOrder,x.IsActive)).ToArrayAsync();}
    [HttpGet("surfaces")] public async Task<DentalSurfaceDto[]> Surfaces() {await access.Doctor();return await db.Set<DentalSurface>().OrderBy(x=>x.DisplayOrder).Select(x=>new DentalSurfaceDto(x.Id,x.Code,x.Name,x.DisplayOrder,x.IsActive)).ToArrayAsync();}
    [HttpGet("finding-types")] public async Task<FindingTypeDto[]> Findings() {await access.Doctor();return await db.Set<OdontogramFindingType>().OrderBy(x=>x.DisplayOrder).Select(x=>new FindingTypeDto(x.Id,x.Code,x.Name,x.Category,x.AppliesToSurface,x.AppliesToWholeTooth,x.DisplayOrder,x.IsActive)).ToArrayAsync();}
    [HttpGet("periodontal-sites")] public async Task<PeriodontalSiteDto[]> Sites() {await access.Doctor();return await db.Set<PeriodontalSite>().OrderBy(x=>x.DisplayOrder).Select(x=>new PeriodontalSiteDto(x.Id,x.Code,x.Name,x.SurfaceGroup,x.DisplayOrder,x.IsActive)).ToArrayAsync();}
}
