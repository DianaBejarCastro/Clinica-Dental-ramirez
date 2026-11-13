using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Application.Patients;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.Patients;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.Appointments;
public sealed partial class AppointmentsService
{
    private const int PatientDuration = 45;
    private static DateTime BookingNow => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"America/La_Paz");
    private sealed record BookableDoctor(Guid DoctorId,Guid SpecialtyId,PatientBookingDoctor Display);
    private async Task<BookableDoctor> BookingDoctor(Guid featuredId,CancellationToken ct)
    {
        var selection=await db.ClinicFeaturedDoctors.AsNoTracking().Where(x=>x.Id==featuredId && x.IsActive && x.ClinicId==Clinic.SingletonId && x.Doctor.IsActive && x.Doctor.User.IsActive
            && x.Doctor.DoctorCenters.Any(c=>c.IsActive && c.Center.IsActive && c.Center.ClinicId==Clinic.SingletonId && c.Center.Clinic.IsActive))
            .Select(x=>new { x.DoctorProfileId,Name=x.Doctor.User.FirstName+" "+x.Doctor.User.LastName,
                Photo=db.UserProfilePhotos.Where(p=>p.UserId==x.Doctor.UserId).Select(p=>(DateTime?)p.UpdatedAt).FirstOrDefault() }).SingleOrDefaultAsync(ct)
            ?? throw new ApiException(404,"Este profesional no está disponible para reservas.");
        // Match the first active specialty in the public card's established alphabetical order.
        var specialty=await db.DoctorSpecialties.Where(x=>x.DoctorId==selection.DoctorProfileId && x.Specialty.IsActive).OrderBy(x=>x.Specialty.Name).ThenBy(x=>x.SpecialtyId)
            .Select(x=>new{x.SpecialtyId,x.Specialty.Name}).FirstOrDefaultAsync(ct) ?? throw new ApiException(400,"El profesional no tiene una especialidad disponible.");
        return new(selection.DoctorProfileId,specialty.SpecialtyId,new(featuredId,selection.Name,specialty.Name,selection.Photo));
    }
    private async Task<PatientAppointmentResponse?> ExistingBooking(Guid patient,CancellationToken ct)
    {
        var now=BookingNow;var date=DateOnly.FromDateTime(now);var time=TimeOnly.FromDateTime(now);
        return await (from a in db.Appointments.AsNoTracking()
            join d in db.DoctorProfiles on a.DoctorId equals d.Id
            join u in db.Users on d.UserId equals u.Id
            join c in db.Centers on a.CenterId equals c.Id
            join s in db.Specialties on a.SpecialtyId equals s.Id
            where a.PatientId==patient && (a.AppointmentDate>date || a.AppointmentDate==date && a.EndTime>time)
                && (a.Status=="SCHEDULED" || a.Status=="CONFIRMED" || a.Status=="IN_PROGRESS")
            orderby a.AppointmentDate,a.StartTime,a.Id
            select new PatientAppointmentResponse(a.Id,a.AppointmentDate,a.StartTime,a.EndTime,a.Status,u.FirstName+" "+u.LastName,c.Name,s.Name,a.Reason,a.CreatedAt)).FirstOrDefaultAsync(ct);
    }
    private async Task<PatientBookingSlot[]> BookingSlots(Guid doctor,DateOnly from,DateOnly to,CancellationToken ct)
    {
        var blocks=await db.DoctorSchedules.AsNoTracking().Where(x=>x.DoctorId==doctor && x.IsActive && x.DoctorCenter.IsActive && x.Center.IsActive && x.Center.ClinicId==Clinic.SingletonId && x.Center.Clinic.IsActive)
            .Select(x=>new { Block=x,Name=x.Center.Name }).ToArrayAsync(ct);
        var occupied=await db.Appointments.AsNoTracking().Where(x=>x.DoctorId==doctor && x.AppointmentDate>=from && x.AppointmentDate<=to && x.Status!=AppointmentStatuses.Cancelled && x.Status!=AppointmentStatuses.NoShow)
            .Select(x=>new{x.AppointmentDate,x.StartTime,x.EndTime}).ToArrayAsync(ct);
        var now=BookingNow;var today=DateOnly.FromDateTime(now);var current=TimeOnly.FromDateTime(now);var result=new List<PatientBookingSlot>();
        for(var date=from;date<=to;date=date.AddDays(1))
        {
            if(date<today)continue;var day=((int)date.DayOfWeek+6)%7+1;
            foreach(var group in blocks.Where(x=>x.Block.DayOfWeek==day).GroupBy(x=>new{x.Block.CenterId,x.Name}))
            {
                var busy=occupied.Where(x=>x.AppointmentDate==date).Select(x=>new DTOs.Appointments.AvailabilitySlot(x.StartTime,x.EndTime)).ToArray();
                foreach(var slot in CalculateSlots(group.Select(x=>x.Block),busy,PatientDuration,PatientDuration))
                    if(date>today || slot.StartTime>current)result.Add(new(date,slot.StartTime,slot.EndTime,group.Key.CenterId,group.Key.Name));
            }
        }
        return result.Distinct().OrderBy(x=>x.Date).ThenBy(x=>x.StartTime).ThenBy(x=>x.CenterId).ToArray();
    }
    public async Task<PatientBookingAvailability> PatientAvailability(Guid featuredId,int year,int month,CancellationToken ct)
    {
        var patient=await new PatientPortalService(db,support).OwnProfile(ct);var now=BookingNow;
        if(month<1||month>12||year<now.Year||year>now.Year+2)throw new ApiException(400,"Mes fuera del período de reservas.");
        var doctor=await BookingDoctor(featuredId,ct);var existing=await ExistingBooking(patient,ct);
        var from=new DateOnly(year,month,1);var to=from.AddMonths(1).AddDays(-1);
        return new(doctor.Display,existing,existing is null?await BookingSlots(doctor.DoctorId,from,to,ct):[]);
    }
    public async Task<PatientAppointmentResponse> PatientBook(PatientBookingRequest request,CancellationToken ct)
    {
        // Every check, including one pending appointment, shares the existing agenda lock.
        var patient=await new PatientPortalService(db,support).OwnProfile(ct);Appointment? appointment=null;BookableDoctor? doctor=null;
        await Mutate(async()=>{
            patient=await new PatientPortalService(db,support).OwnProfile(ct);
            doctor=await BookingDoctor(request.FeaturedDoctorId,ct);
            if(await ExistingBooking(patient,ct) is not null)throw new ApiException(409,"Ya tienes una cita reservada. Consulta Mis citas.");
            var now=BookingNow;
            if(request.Date==default || request.Date<DateOnly.FromDateTime(now) || request.Date.Year>now.Year+2 || request.Reason?.Length>1000)
                throw new ApiException(400,"Fecha o motivo inválido.");
            var slot=(await BookingSlots(doctor.DoctorId,request.Date,request.Date,ct)).SingleOrDefault(x=>x.StartTime==request.StartTime && x.CenterId==request.CenterId)
                ?? throw new ApiException(409,"Ese horario ya no está disponible. Selecciona otro horario.");
            appointment=new(){PatientId=patient,DoctorId=doctor.DoctorId,CenterId=slot.CenterId,SpecialtyId=doctor.SpecialtyId,AppointmentDate=slot.Date,StartTime=slot.StartTime,EndTime=slot.EndTime,
                Reason=string.IsNullOrWhiteSpace(request.Reason)?null:request.Reason.Trim(),CreatedByUserId=support.Actor};
            await Validate(appointment);db.Appointments.Add(appointment);support.Audit("appointment_created","Appointment",appointment.Id);
        });
        var saved=await Get(appointment!.Id);
        return new(saved.Id,saved.AppointmentDate,saved.StartTime,saved.EndTime,saved.Status,doctor!.Display.Name,saved.CenterName,doctor.Display.Specialty,saved.Reason,saved.CreatedAt);
    }
}
