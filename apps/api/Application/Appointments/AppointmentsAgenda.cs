using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Appointments;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.Appointments;

public sealed partial class AppointmentsService
{
    public async Task<AgendaResponse> Agenda(DateOnly date, Guid? doctorId, Guid? centerId, Guid? specialtyId, string? status)
    {
        if(date == default || (status != null && !ClinicaRamirez.Api.Domain.Appointments.AppointmentStatuses.IsValid(status)))
            throw new ApiException(400, "Fecha o estado inválido.");
        var appointments = db.Appointments.AsNoTracking().Where(a => a.AppointmentDate == date
            && (!doctorId.HasValue || a.DoctorId == doctorId)
            && (!centerId.HasValue || a.CenterId == centerId)
            && (!specialtyId.HasValue || a.SpecialtyId == specialtyId));
        var bookedDoctors = appointments.Select(a => a.DoctorId);
        var doctors = db.DoctorProfiles.AsNoTracking().Where(d =>
            (!doctorId.HasValue || d.Id == doctorId)
            && (bookedDoctors.Contains(d.Id) || (d.IsActive && d.User.IsActive
                && (!centerId.HasValue || d.DoctorCenters.Any(c => c.CenterId == centerId && c.IsActive && c.Center.IsActive))
                && (!specialtyId.HasValue || d.DoctorSpecialties.Any(s => s.SpecialtyId == specialtyId)))));
        var ids = doctors.Select(d => d.Id);
        var day = ((int)date.DayOfWeek + 6) % 7 + 1;
        var intervals = await db.DoctorSchedules.AsNoTracking().Where(s => ids.Contains(s.DoctorId)
            && s.IsActive && s.Doctor.IsActive && s.Doctor.User.IsActive && s.DoctorCenter.IsActive && s.Center.IsActive
            && s.DayOfWeek == day && (!centerId.HasValue || s.CenterId == centerId))
            .OrderBy(s=>s.StartTime).Select(s => new AgendaInterval(s.DoctorId,s.CenterId,s.Center.Name,s.StartTime,s.EndTime,
                s.HasBreak?s.BreakStart:null,s.HasBreak?s.BreakEnd:null)).ToArrayAsync();
        var headers = await doctors.OrderBy(d=>d.User.FirstName).ThenBy(d=>d.Id)
            .Select(d => new AgendaDoctor(d.Id,d.UserId,d.User.FirstName,d.User.LastName,
                d.DoctorSpecialties.OrderBy(s=>s.Specialty.Name).Select(s=>s.Specialty.Name).ToArray())).ToArrayAsync();
        var entries = await appointments.Where(a=>status==null || a.Status==status)
            .OrderBy(a=>a.StartTime).ThenBy(a=>a.Id).Select(Response).ToArrayAsync();
        return new(date,30,headers,intervals,entries);
    }
}
