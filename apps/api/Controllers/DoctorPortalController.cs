using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Doctors;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Controllers;

[ApiController, Authorize, Route("api/doctor/me/appointments")]
public sealed class DoctorPortalController(AppDbContext db, IdentitySupport support) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DoctorAgenda>> Get([FromQuery] DateOnly date, [FromQuery] string view = "day", CancellationToken ct = default)
    {
        var userId = support.Actor ?? throw new ApiException(401, "Inicia sesión para continuar.");
        if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && x.User.IsActive && x.Role.IsActive && x.Role.Code == "DOCTOR", ct))
            throw new ApiException(403, "Este portal es exclusivo para doctores.");
        var doctorId = await db.DoctorProfiles.Where(x => x.UserId == userId && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(403, "No hay un perfil de doctor activo vinculado a esta cuenta.");
        if (date.Year < 1900 || date.Year > 9998 || view is not ("day" or "week" or "month"))
            throw new ApiException(400, "Selecciona una fecha válida y una vista día, semana o mes.");
        var rangeStart = view switch { "week" => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)), "month" => new DateOnly(date.Year, date.Month, 1), _ => date };
        var to = view switch { "week" => rangeStart.AddDays(7), "month" => rangeStart.AddMonths(1), _ => rangeStart.AddDays(1) };
        var items = await (from a in db.Appointments.AsNoTracking()
            where a.DoctorId == doctorId && a.AppointmentDate >= rangeStart && a.AppointmentDate < to
            join p in db.PatientProfiles on a.PatientId equals p.Id
            join u in db.Users on p.UserId equals u.Id
            join c in db.Centers on a.CenterId equals c.Id
            join s in db.Specialties on a.SpecialtyId equals s.Id
            orderby a.AppointmentDate, a.StartTime, a.Id
            select new DoctorAppointment(a.Id, a.AppointmentDate, a.StartTime, a.EndTime,
                new DoctorPatient(p.Id, u.FirstName, u.LastName), new DoctorAppointmentLocation(s.Id, s.Name),
                new DoctorAppointmentLocation(c.Id, c.Name), a.Reason, a.Status)).ToArrayAsync(ct);
        return Ok(new DoctorAgenda(rangeStart, to, items));
    }
}
