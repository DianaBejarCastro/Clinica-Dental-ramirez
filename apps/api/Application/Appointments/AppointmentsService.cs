using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.Appointments;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.Appointments;

public sealed class AppointmentsService(AppDbContext db, IdentitySupport support)
{
    private async Task Mutate(Func<Task> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        // Comparte el bloqueo con horarios, asignaciones y desactivaciones.
        await support.LockAdministration();
        await action();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    private async Task<Appointment> Find(Guid id) => await db.Appointments.SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiException(404, "Cita inexistente.");
    public async Task<AppointmentListResponse> List(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ApiException(400, "Paginación inválida.");
        var query = db.Appointments.AsNoTracking();
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.AppointmentDate).ThenBy(x => x.StartTime).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(Response).ToArrayAsync();
        return new(items, total, page, pageSize);
    }
    public async Task<AppointmentResponse> Get(Guid id) => await db.Appointments.AsNoTracking().Where(x => x.Id == id).Select(Response).SingleOrDefaultAsync()
        ?? throw new ApiException(404, "Cita inexistente.");
    public async Task<AppointmentResponse> Create(AppointmentCreateRequest request)
    {
        var appointment = new Appointment { CreatedByUserId = support.Actor };
        await Mutate(async () => {
            Apply(appointment, request);
            await Validate(appointment);
            db.Appointments.Add(appointment);
            support.Audit("appointment_created", "Appointment", appointment.Id);
        });
        return await Get(appointment.Id);
    }
    public async Task<AppointmentResponse> Update(Guid id, AppointmentUpdateRequest request)
    {
        await Mutate(async () => {
            var appointment = await Find(id);
            Apply(appointment, request);
            await Validate(appointment);
            support.Audit("appointment_updated", "Appointment", id);
        });
        return await Get(id);
    }
    public Task Status(Guid id, string status) => Mutate(async () => {
        if (!AppointmentStatuses.IsValid(status)) throw new ApiException(400, "Estado de cita inválido.");
        var permission = status == AppointmentStatuses.Cancelled ? "appointments.cancel" : "appointments.update";
        if (!await db.UserRoles.AnyAsync(x => x.UserId == support.Actor && x.User.IsActive && x.Role.IsActive
            && x.Role.RolePermissions.Any(p => p.Permission.Code == permission)))
            throw new ApiException(403, "No tiene permiso para cambiar el estado de la cita.");
        var appointment = await Find(id);
        var previous = appointment.Status;
        if (previous == status) return;
        appointment.Status = status;
        // Cancelar/marcar ausencia sigue siendo posible tras desactivar una relación.
        // Volver a un estado que ocupa tiempo exige revalidar la reserva completa.
        if (AppointmentStatuses.Blocks(status)) await Validate(appointment);
        support.Audit("appointment_status_changed", "Appointment", id, metadata: new { previous, status });
        if (status == AppointmentStatuses.Cancelled)
            support.Audit("appointment_cancelled", "Appointment", id);
    });
    private async Task ValidateResources(Guid doctorId, Guid centerId, Guid specialtyId)
    {
        var doctor = await db.DoctorProfiles.AsNoTracking().Include(x => x.User).SingleOrDefaultAsync(x => x.Id == doctorId)
            ?? throw new ApiException(404, "Doctor inexistente.");
        if (!doctor.IsActive || !doctor.User.IsActive) throw new ApiException(400, "Doctor inactivo.");
        var center = await db.Centers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == centerId)
            ?? throw new ApiException(404, "Sucursal inexistente.");
        if (!center.IsActive) throw new ApiException(400, "Sucursal inactiva.");
        var specialty = await db.Specialties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == specialtyId)
            ?? throw new ApiException(404, "Especialidad inexistente.");
        if (!specialty.IsActive) throw new ApiException(400, "Especialidad inactiva.");
        if (!await db.DoctorCenters.AnyAsync(x => x.DoctorId == doctorId && x.CenterId == centerId && x.IsActive))
            throw new ApiException(400, "Doctor no asignado a la sucursal activa.");
        if (!await db.DoctorSpecialties.AnyAsync(x => x.DoctorId == doctorId && x.SpecialtyId == specialtyId))
            throw new ApiException(400, "Especialidad no asignada al doctor.");
    }
    private Task<DoctorSchedule[]> Schedules(Guid doctorId, Guid centerId, DateOnly date)
    {
        var day = ((int)date.DayOfWeek + 6) % 7 + 1;
        return db.DoctorSchedules.AsNoTracking().Where(x => x.DoctorId == doctorId && x.CenterId == centerId
            && x.IsActive && x.DayOfWeek == day).OrderBy(x => x.StartTime).ToArrayAsync();
    }
    private IQueryable<Appointment> Occupied(Guid doctorId, DateOnly date) => db.Appointments.AsNoTracking()
        .Where(x => x.DoctorId == doctorId && x.AppointmentDate == date
            && x.Status != AppointmentStatuses.Cancelled && x.Status != AppointmentStatuses.NoShow);
    private static bool Fits(DoctorSchedule block, TimeOnly start, TimeOnly end) =>
        start >= block.StartTime && end <= block.EndTime
        && (!block.HasBreak || end <= block.BreakStart!.Value || start >= block.BreakEnd!.Value);
    private async Task Validate(Appointment a)
    {
        if (a.AppointmentDate == default || a.StartTime >= a.EndTime) throw new ApiException(400, "Fecha u horas inválidas.");
        var patient = await db.PatientProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == a.PatientId)
            ?? throw new ApiException(404, "Paciente inexistente.");
        if (!patient.IsActive) throw new ApiException(400, "Paciente inactivo.");
        await ValidateResources(a.DoctorId, a.CenterId, a.SpecialtyId);
        if (!(await Schedules(a.DoctorId, a.CenterId, a.AppointmentDate)).Any(x => Fits(x, a.StartTime, a.EndTime)))
            throw new ApiException(400, "Cita fuera del horario activo o dentro del descanso.");
        if (AppointmentStatuses.Blocks(a.Status) && await Occupied(a.DoctorId, a.AppointmentDate)
            .AnyAsync(x => x.Id != a.Id && x.StartTime < a.EndTime && a.StartTime < x.EndTime))
            throw new ApiException(409, "El doctor ya tiene una cita en ese intervalo.");
    }
    public async Task<AvailabilityResponse> Availability(Guid doctorId, Guid centerId, Guid specialtyId, DateOnly date, int durationMinutes)
    {
        if (date == default || durationMinutes < 1 || durationMinutes > 1440)
            throw new ApiException(400, "Fecha o duración inválida.");
        await ValidateResources(doctorId, centerId, specialtyId);
        var blocks = await Schedules(doctorId, centerId, date);
        var occupied = await Occupied(doctorId, date).ToArrayAsync();
        var slots = new List<AvailabilitySlot>();
        var duration = TimeSpan.FromMinutes(durationMinutes).Ticks;
        foreach (var block in blocks)
            for (var ticks = block.StartTime.Ticks; ticks + duration <= block.EndTime.Ticks; ticks += TimeSpan.FromMinutes(30).Ticks)
            {
                var start = new TimeOnly(ticks); var end = new TimeOnly(ticks + duration);
                if (Fits(block, start, end) && !occupied.Any(x => x.StartTime < end && start < x.EndTime))
                    slots.Add(new(start, end));
            }
        return new(doctorId, centerId, specialtyId, date, durationMinutes,
            slots.Distinct().OrderBy(x => x.StartTime).ToArray());
    }
    private static void Apply(Appointment a, AppointmentUpdateRequest r)
    {
        a.PatientId = r.PatientId; a.DoctorId = r.DoctorId; a.CenterId = r.CenterId; a.SpecialtyId = r.SpecialtyId;
        a.AppointmentDate = r.AppointmentDate; a.StartTime = r.StartTime; a.EndTime = r.EndTime;
        a.Reason = string.IsNullOrWhiteSpace(r.Reason) ? null : r.Reason.Trim();
        a.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
    }
    private System.Linq.Expressions.Expression<Func<Appointment, AppointmentResponse>> Response => a => new AppointmentResponse(a.Id, a.PatientId, a.DoctorId, a.CenterId, a.SpecialtyId,
        a.AppointmentDate, a.StartTime, a.EndTime, a.Status, a.Reason, a.Notes, a.CreatedByUserId, a.CreatedAt, a.UpdatedAt)
        {
            PatientName = db.PatientProfiles.Where(p => p.Id == a.PatientId).Select(p => p.User.FirstName + " " + p.User.LastName).FirstOrDefault() ?? "",
            DoctorName = db.DoctorProfiles.Where(d => d.Id == a.DoctorId).Select(d => d.User.FirstName + " " + d.User.LastName).FirstOrDefault() ?? "",
            CenterName = db.Centers.Where(c => c.Id == a.CenterId).Select(c => c.Name).FirstOrDefault() ?? ""
        };
}
