using ClinicaRamirez.Api.Application.ClinicalCare;
using ClinicaRamirez.Api.Application.DentalCharting;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.ClinicalCare;
using ClinicaRamirez.Api.DTOs.Doctors;
using ClinicaRamirez.Api.Infrastructure.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Controllers;

[ApiController, Authorize, Route("api/doctor/me/patients")]
public sealed class DoctorPatientsController(AppDbContext db, ChartingAccess access, ClinicalCareService clinical) : ControllerBase
{
    [HttpGet, RequirePermission("patients.view")]
    public async Task<ActionResult<DoctorPatientsPage>> List([FromQuery] string? q, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var doctor = await access.Doctor();
        if (q?.Length > 200 || page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ApiException(400, "Búsqueda o paginación inválida.");
        // A clinical encounter always belongs to an appointment. EXISTS preserves one row per patient.
        // The same relationship is enforced by ChartingAccess for dental history.
        var query = db.PatientProfiles.AsNoTracking().Where(p => p.IsActive && p.User.IsActive
            && db.Appointments.Any(a => a.PatientId == p.Id && a.DoctorId == doctor));
        foreach (var word in (q ?? "").Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = word;
            query = query.Where(p => (p.User.FirstName + " " + p.User.LastName).ToLower().Contains(token)
                || (p.User.DocumentNumber != null && p.User.DocumentNumber.ToLower().Contains(token))
                || (p.User.Phone != null && p.User.Phone.Contains(token)));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.User.LastName).ThenBy(p => p.User.FirstName).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new DoctorPatientItem(p.Id, p.User.FirstName, p.User.LastName, p.User.DocumentType,
                p.User.DocumentNumber, p.User.DocumentCountryCode, p.User.Phone)).ToArrayAsync(ct);
        return Ok(new DoctorPatientsPage(items, total, page, pageSize));
    }

    [HttpGet("{patientId:guid}/clinical"), RequirePermission("clinical_history.view")]
    public async Task<ActionResult<ClinicalContext>> History(Guid patientId, CancellationToken ct)
    {
        // Historical access remains available for inactive patients already attended by this doctor,
        // as in the existing appointment and charting history endpoints. No write is performed here.
        var doctor = await access.Patient(patientId);
        var appointment = await db.Appointments.AsNoTracking().Where(a => a.PatientId == patientId && a.DoctorId == doctor)
            .OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.StartTime).ThenBy(a => a.Id)
            .Select(a => a.Id).FirstAsync(ct);
        return Ok(await clinical.Get(appointment));
    }
}
