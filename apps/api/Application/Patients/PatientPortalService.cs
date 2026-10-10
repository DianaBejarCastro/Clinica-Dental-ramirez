using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.Patients;
public sealed class PatientPortalService(AppDbContext db, IdentitySupport support)
{
    private async Task<Guid> OwnProfile(CancellationToken ct)
    {
        var userId = support.Actor ?? throw new ApiException(401, "Inicia sesión para continuar.");
        if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && x.User.IsActive && x.Role.IsActive && x.Role.Code == "PATIENT", ct))
            throw new ApiException(403, "Este portal es exclusivo para pacientes.");
        return await db.PatientProfiles.Where(x => x.UserId == userId && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(403, "No hay un perfil de paciente activo vinculado a esta cuenta.");
    }
    public async Task<PatientAccountResponse> Me(CancellationToken ct)
    {
        var id = await OwnProfile(ct);
        return await db.PatientProfiles.AsNoTracking().Where(x => x.Id == id).Select(x => new PatientAccountResponse(
            x.FirstName, x.LastName, x.Ci, x.BirthDate, x.Gender, x.Phone, x.Email)).SingleAsync(ct);
    }
    public async Task<PatientAppointmentResponse[]> Appointments(CancellationToken ct)
    {
        var id = await OwnProfile(ct);
        return await (from a in db.Appointments.AsNoTracking() where a.PatientId == id
            join d in db.DoctorProfiles on a.DoctorId equals d.Id
            join u in db.Users on d.UserId equals u.Id
            join c in db.Centers on a.CenterId equals c.Id
            join s in db.Specialties on a.SpecialtyId equals s.Id
            orderby a.AppointmentDate descending, a.StartTime descending
            select new PatientAppointmentResponse(a.Id, a.AppointmentDate, a.StartTime, a.EndTime, a.Status,
                u.FirstName + " " + u.LastName, c.Name, s.Name)).ToArrayAsync(ct);
    }
}
