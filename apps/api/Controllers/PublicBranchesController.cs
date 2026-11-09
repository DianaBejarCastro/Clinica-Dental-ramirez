using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Controllers;

public sealed record PublicBranchResponse(Guid Id, string Name, string Address, string? City,
    string? Department, string? Phone, decimal? Latitude, decimal? Longitude);

[ApiController]
public sealed class PublicBranchesController(AppDbContext db) : ControllerBase
{
    [AllowAnonymous, HttpGet("api/clinic/branches")]
    public async Task<ActionResult<PublicBranchResponse[]>> List(CancellationToken ct) =>
        await db.Centers.AsNoTracking()
            .Where(c => c.ClinicId == Clinic.SingletonId && c.IsActive &&
                db.Clinics.Any(clinic => clinic.Id == c.ClinicId && clinic.IsActive))
            .OrderByDescending(c => c.IsMain).ThenBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new PublicBranchResponse(c.Id, c.Name, c.Address, c.City,
                c.Department, c.Phone, c.Latitude, c.Longitude)).ToArrayAsync(ct);
}
