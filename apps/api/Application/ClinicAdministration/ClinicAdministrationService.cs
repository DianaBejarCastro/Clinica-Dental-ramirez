using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.ClinicAdministration;

public sealed class ClinicAdministrationService(AppDbContext db, IdentitySupport support, IPasswordHasher<User> hasher)
{
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Text(string value) => Optional(value) ?? throw new ApiException(400, "El texto obligatorio no puede estar vacío.");
    // A single transaction lock coordinates administration invariants across API instances.
    // This also coordinates role changes in the identity module; it does not lock read requests.
    private async Task Mutate(Func<Task> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        await action();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    private async Task StatusPermission(bool previous, bool next, string permission)
    {
        if (previous == next) return;
        if (!await db.UserRoles.AnyAsync(x => x.UserId == support.Actor && x.User.IsActive && x.Role.IsActive
            && x.Role.RolePermissions.Any(p => p.Permission.Code == permission)))
            throw new ApiException(403, "No tiene permiso para cambiar el estado.");
    }
    private IQueryable<DoctorProfile> DoctorQuery => db.DoctorProfiles.Include(x => x.User)
        .Include(x => x.DoctorSpecialties).ThenInclude(x => x.Specialty)
        .Include(x => x.DoctorCenters).ThenInclude(x => x.Center).AsSplitQuery();
    private async Task<DoctorProfile> DoctorEntity(Guid id) => await db.DoctorProfiles.SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiException(404, "Doctor inexistente.");
    private static void ActiveDoctor(DoctorProfile doctor)
    {
        if (!doctor.IsActive) throw new ApiException(400, "El doctor está inactivo.");
    }
    public async Task<ClinicResponse> Clinic() => ClinicDto(await db.Clinics.AsNoTracking().SingleOrDefaultAsync()
        ?? throw new ApiException(404, "La clínica no está inicializada. Ejecute el seed explícito."));
    public async Task<ClinicResponse> UpdateClinic(ClinicUpdateRequest request)
    {
        await Mutate(async () => {
            var clinic = await db.Clinics.SingleOrDefaultAsync() ?? throw new ApiException(404, "Clínica inexistente.");
            clinic.Name = Text(request.Name); clinic.LegalName = Optional(request.LegalName);
            clinic.Description = Optional(request.Description); clinic.LogoUrl = Optional(request.LogoUrl);
            clinic.Phone = Optional(request.Phone); clinic.Whatsapp = Optional(request.Whatsapp);
            clinic.Email = Optional(request.Email)?.ToLowerInvariant(); clinic.Website = Optional(request.Website);
            clinic.TaxId = Optional(request.TaxId); clinic.IsActive = request.IsActive;
            support.Audit("clinic_updated", "Clinic", clinic.Id);
        });
        return await Clinic();
    }
    public async Task<CenterResponse[]> Centers() => (await db.Centers.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync()).Select(CenterDto).ToArray();
    public async Task<CenterResponse> Center(Guid id) => CenterDto(await db.Centers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiException(404, "Sucursal inexistente."));
    public async Task<CenterResponse> CreateCenter(CenterCreateRequest request)
    {
        var center = new Center();
        await Mutate(async () => {
            center.ClinicId = (await db.Clinics.SingleOrDefaultAsync() ?? throw new ApiException(400, "Inicialice la clínica.")).Id;
            await ApplyCenter(center, request);
            db.Centers.Add(center);
            support.Audit("center_created", "Center", center.Id);
        });
        return await Center(center.Id);
    }
    public async Task<CenterResponse> UpdateCenter(Guid id, CenterUpdateRequest request)
    {
        await Mutate(async () => {
            var center = await db.Centers.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Sucursal inexistente.");
            await StatusPermission(center.IsActive, request.IsActive, "centers.disable");
            var changedStatus = center.IsActive != request.IsActive;
            await ApplyCenter(center, request);
            if (!center.IsActive) await DisableSchedules(db.DoctorSchedules.Where(x => x.CenterId == id), "center_inactive");
            support.Audit("center_updated", "Center", id);
            if (changedStatus) support.Audit("center_status_changed", "Center", id, metadata: new { isActive = center.IsActive });
        });
        return await Center(id);
    }
    public Task CenterStatus(Guid id, bool active) => Mutate(async () => {
        var center = await db.Centers.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Sucursal inexistente.");
        await CheckMain(id, center.ClinicId, center.IsMain, active);
        center.IsActive = active;
        if (!active) await DisableSchedules(db.DoctorSchedules.Where(x => x.CenterId == id), "center_inactive");
        support.Audit("center_status_changed", "Center", id, metadata: new { isActive = active });
    });
    private async Task CheckMain(Guid id, Guid clinicId, bool main, bool active)
    {
        if (main && active && await db.Centers.AnyAsync(x => x.Id != id && x.ClinicId == clinicId && x.IsMain && x.IsActive))
            throw new ApiException(409, "Ya existe una sucursal principal activa.");
    }
    private async Task ApplyCenter(Center center, CenterUpdateRequest request)
    {
        var code = Text(request.Code).ToUpperInvariant();
        if (await db.Centers.AnyAsync(x => x.Id != center.Id && x.Code == code)) throw new ApiException(409, "El código de sucursal ya existe.");
        await CheckMain(center.Id, center.ClinicId, request.IsMain, request.IsActive);
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180) throw new ApiException(400, "Coordenadas inválidas.");
        center.Name = Text(request.Name); center.Code = code; center.Description = Optional(request.Description);
        center.Address = Text(request.Address); center.City = Optional(request.City); center.Department = Optional(request.Department);
        center.Country = Text(request.Country); center.Phone = Optional(request.Phone); center.Whatsapp = Optional(request.Whatsapp);
        center.Email = Optional(request.Email)?.ToLowerInvariant(); center.GoogleMapsUrl = Optional(request.GoogleMapsUrl);
        center.Latitude = request.Latitude; center.Longitude = request.Longitude; center.IsMain = request.IsMain; center.IsActive = request.IsActive;
    }
    public async Task<SpecialtyResponse[]> Specialties() => (await db.Specialties.AsNoTracking()
        .Include(x => x.DoctorSpecialties).OrderBy(x => x.Name).ToArrayAsync()).Select(SpecialtyDto).ToArray();
    public async Task<SpecialtyResponse> Specialty(Guid id) => SpecialtyDto(await db.Specialties.AsNoTracking()
        .Include(x => x.DoctorSpecialties).SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Especialidad inexistente."));
    public async Task<SpecialtyResponse> CreateSpecialty(SpecialtyCreateRequest request)
    {
        var specialty = new Specialty();
        await Mutate(async () => {
            await ApplySpecialty(specialty, request); db.Specialties.Add(specialty);
            support.Audit("specialty_created", "Specialty", specialty.Id);
        });
        return await Specialty(specialty.Id);
    }
    public async Task<SpecialtyResponse> UpdateSpecialty(Guid id, SpecialtyUpdateRequest request)
    {
        await Mutate(async () => {
            var specialty = await db.Specialties.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Especialidad inexistente.");
            await StatusPermission(specialty.IsActive, request.IsActive, "specialties.disable");
            var changedStatus = specialty.IsActive != request.IsActive;
            await ApplySpecialty(specialty, request);
            support.Audit("specialty_updated", "Specialty", id);
            if (changedStatus) support.Audit("specialty_status_changed", "Specialty", id, metadata: new { isActive = specialty.IsActive });
        });
        return await Specialty(id);
    }
    public Task SpecialtyStatus(Guid id, bool active) => Mutate(async () => {
        var specialty = await db.Specialties.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Especialidad inexistente.");
        specialty.IsActive = active;
        support.Audit("specialty_status_changed", "Specialty", id, metadata: new { isActive = active });
    });
    private async Task ApplySpecialty(Specialty specialty, SpecialtyUpdateRequest request)
    {
        var code = Text(request.Code).ToUpperInvariant(); var name = Text(request.Name);
        if (await db.Specialties.AnyAsync(x => x.Id != specialty.Id && (x.Code == code || x.Name.ToLower() == name.ToLower())))
            throw new ApiException(409, "El código o nombre de especialidad ya existe.");
        specialty.Code = code; specialty.Name = name; specialty.Description = Optional(request.Description); specialty.IsActive = request.IsActive;
    }
    public async Task<DoctorListResponse> Doctors(int page, int pageSize)
    {
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100) throw new ApiException(400, "Paginación inválida.");
        var doctors = await DoctorQuery.AsNoTracking().OrderBy(x => x.User.LastName).ThenBy(x => x.User.FirstName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return new(doctors.Select(DoctorDto).ToArray(), await db.DoctorProfiles.CountAsync(), page, pageSize);
    }
    public async Task<DoctorResponse> Doctor(Guid id) => DoctorDto(await DoctorQuery.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiException(404, "Doctor inexistente."));
    public async Task<DoctorResponse> CreateDoctor(DoctorCreateRequest request)
    {
        var doctor = new DoctorProfile();
        await Mutate(async () => {
            if ((request.UserId is null) == (request.NewUser is null)) throw new ApiException(400, "Seleccione un usuario existente o cree uno nuevo.");
            var role = await db.Roles.SingleOrDefaultAsync(x => x.Code == "DOCTOR" && x.IsActive)
                ?? throw new ApiException(409, "El rol DOCTOR no está disponible.");
            User user;
            if (request.UserId is Guid userId)
            {
                user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId) ?? throw new ApiException(404, "Usuario inexistente.");
                if (await db.DoctorProfiles.AnyAsync(x => x.UserId == userId)) throw new ApiException(409, "El usuario ya tiene un perfil profesional.");
            }
            else
            {
                var data = request.NewUser!;
                var email = Text(data.Email).ToLowerInvariant();
                if (await db.Users.AnyAsync(x => x.Email == email)) throw new ApiException(409, "El email ya existe; vincule el usuario existente.");
                user = new User { FirstName = Text(data.FirstName), LastName = Text(data.LastName), Email = email, Phone = Optional(data.Phone) };
                user.PasswordHash = hasher.HashPassword(user, data.Password);
                db.Users.Add(user); support.Audit("user_created", "User", user.Id);
            }
            if (!await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.RoleId == role.Id))
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedByUserId = support.Actor });
                support.Audit("user_roles_updated", "User", user.Id, metadata: new { roleId = role.Id });
            }
            doctor.UserId = user.Id;
            await ApplyDoctor(doctor, request);
            db.DoctorProfiles.Add(doctor); support.Audit("doctor_created", "DoctorProfile", doctor.Id, metadata: new { userId = user.Id });
        });
        return await Doctor(doctor.Id);
    }
    public async Task<DoctorResponse> UpdateDoctor(Guid id, DoctorUpdateRequest request)
    {
        await Mutate(async () => {
            var doctor = await DoctorEntity(id);
            await StatusPermission(doctor.IsActive, request.IsActive, "doctors.disable");
            var changedStatus = doctor.IsActive != request.IsActive;
            await ApplyDoctor(doctor, request);
            if (!doctor.IsActive) await DisableSchedules(db.DoctorSchedules.Where(x => x.DoctorId == id), "doctor_inactive");
            support.Audit("doctor_updated", "DoctorProfile", id);
            if (changedStatus) support.Audit("doctor_status_changed", "DoctorProfile", id, metadata: new { isActive = doctor.IsActive });
        });
        return await Doctor(id);
    }
    public Task DoctorStatus(Guid id, bool active) => Mutate(async () => {
        var doctor = await DoctorEntity(id); doctor.IsActive = active;
        if (!active) await DisableSchedules(db.DoctorSchedules.Where(x => x.DoctorId == id), "doctor_inactive");
        support.Audit("doctor_status_changed", "DoctorProfile", id, metadata: new { isActive = active });
    });
    private async Task ApplyDoctor(DoctorProfile doctor, DoctorUpdateRequest request)
    {
        var license = Optional(request.ProfessionalLicense)?.ToUpperInvariant();
        if (license is not null && await db.DoctorProfiles.AnyAsync(x => x.Id != doctor.Id && x.ProfessionalLicense == license))
            throw new ApiException(409, "La matrícula profesional ya existe.");
        if (request.BirthDate > DateOnly.FromDateTime(DateTime.UtcNow)) throw new ApiException(400, "La fecha de nacimiento no puede ser futura.");
        doctor.ProfessionalLicense = license; doctor.Ci = Optional(request.Ci); doctor.BirthDate = request.BirthDate;
        doctor.Gender = Optional(request.Gender); doctor.Phone = Optional(request.Phone); doctor.Address = Optional(request.Address);
        doctor.PhotoUrl = Optional(request.PhotoUrl); doctor.Bio = Optional(request.Bio); doctor.IsActive = request.IsActive;
    }
    public async Task<AssignmentResponse[]> DoctorSpecialties(Guid doctorId)
    {
        await DoctorEntity(doctorId);
        return await db.DoctorSpecialties.AsNoTracking().Where(x => x.DoctorId == doctorId).OrderBy(x => x.Specialty.Name)
            .Select(x => new AssignmentResponse(x.SpecialtyId, x.Specialty.Code, x.Specialty.Name, x.Specialty.IsActive)).ToArrayAsync();
    }
    public Task AssignSpecialties(Guid doctorId, Guid[] values) => Mutate(async () => {
        ActiveDoctor(await DoctorEntity(doctorId));
        var ids = values.Distinct().ToArray();
        if (await db.Specialties.CountAsync(x => ids.Contains(x.Id) && x.IsActive) != ids.Length)
            throw new ApiException(400, "Especialidades inexistentes o inactivas.");
        var current = await db.DoctorSpecialties.Where(x => x.DoctorId == doctorId).ToArrayAsync();
        db.DoctorSpecialties.RemoveRange(current.Where(x => !ids.Contains(x.SpecialtyId)));
        foreach (var id in ids.Where(id => current.All(x => x.SpecialtyId != id)))
            db.DoctorSpecialties.Add(new DoctorSpecialty { DoctorId = doctorId, SpecialtyId = id });
        support.Audit("doctor_specialties_updated", "DoctorProfile", doctorId, metadata: new { specialtyIds = ids });
    });
    public async Task<AssignmentResponse[]> DoctorCenters(Guid doctorId)
    {
        await DoctorEntity(doctorId);
        return await db.DoctorCenters.AsNoTracking().Where(x => x.DoctorId == doctorId && x.IsActive).OrderBy(x => x.Center.Name)
            .Select(x => new AssignmentResponse(x.CenterId, x.Center.Code, x.Center.Name, x.Center.IsActive)).ToArrayAsync();
    }
    public Task AssignCenters(Guid doctorId, Guid[] values) => Mutate(async () => {
        ActiveDoctor(await DoctorEntity(doctorId));
        var ids = values.Distinct().ToArray();
        if (await db.Centers.CountAsync(x => ids.Contains(x.Id) && x.IsActive) != ids.Length)
            throw new ApiException(400, "Sucursales inexistentes o inactivas.");
        var current = await db.DoctorCenters.Where(x => x.DoctorId == doctorId).ToArrayAsync();
        foreach (var assignment in current) assignment.IsActive = ids.Contains(assignment.CenterId);
        foreach (var id in ids.Where(id => current.All(x => x.CenterId != id)))
            db.DoctorCenters.Add(new DoctorCenter { DoctorId = doctorId, CenterId = id });
        await DisableSchedules(db.DoctorSchedules.Where(x => x.DoctorId == doctorId && !ids.Contains(x.CenterId)), "center_unassigned");
        support.Audit("doctor_centers_updated", "DoctorProfile", doctorId, metadata: new { centerIds = ids });
    });
    public async Task<DoctorScheduleResponse[]> Schedules(Guid doctorId)
    {
        await DoctorEntity(doctorId);
        return (await db.DoctorSchedules.AsNoTracking().Include(x => x.Center).Where(x => x.DoctorId == doctorId)
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime).ToArrayAsync()).Select(ScheduleDto).ToArray();
    }
    public async Task<DoctorScheduleResponse> CreateSchedule(Guid doctorId, DoctorScheduleCreateRequest request)
    {
        var schedule = new DoctorSchedule { DoctorId = doctorId };
        await Mutate(async () => {
            await DoctorEntity(doctorId);
            ApplySchedule(schedule, request);
            await ValidateSchedule(schedule);
            db.DoctorSchedules.Add(schedule);
            support.Audit("schedule_created", "DoctorSchedule", schedule.Id, metadata: new { doctorId, centerId = schedule.CenterId });
        });
        return await Schedule(schedule.Id);
    }
    public async Task<DoctorScheduleResponse> UpdateSchedule(Guid doctorId, Guid scheduleId, DoctorScheduleUpdateRequest request)
    {
        await Mutate(async () => {
            var schedule = await db.DoctorSchedules.SingleOrDefaultAsync(x => x.Id == scheduleId && x.DoctorId == doctorId)
                ?? throw new ApiException(404, "Horario inexistente.");
            await StatusPermission(schedule.IsActive, request.IsActive, "schedules.disable");
            var changedStatus = schedule.IsActive != request.IsActive;
            ApplySchedule(schedule, request);
            await ValidateSchedule(schedule);
            support.Audit("schedule_updated", "DoctorSchedule", schedule.Id, metadata: new { doctorId, centerId = schedule.CenterId });
            if (changedStatus) support.Audit("schedule_status_changed", "DoctorSchedule", schedule.Id, metadata: new { isActive = schedule.IsActive });
        });
        return await Schedule(scheduleId);
    }
    public Task ScheduleStatus(Guid doctorId, Guid scheduleId, bool active) => Mutate(async () => {
        var schedule = await db.DoctorSchedules.SingleOrDefaultAsync(x => x.Id == scheduleId && x.DoctorId == doctorId)
            ?? throw new ApiException(404, "Horario inexistente.");
        schedule.IsActive = active;
        if (active) await ValidateSchedule(schedule);
        support.Audit("schedule_status_changed", "DoctorSchedule", scheduleId, metadata: new { doctorId, isActive = active });
    });
    private static void ApplySchedule(DoctorSchedule schedule, DoctorScheduleUpdateRequest request)
    {
        schedule.CenterId = request.CenterId; schedule.DayOfWeek = request.DayOfWeek;
        schedule.StartTime = request.StartTime; schedule.EndTime = request.EndTime; schedule.HasBreak = request.HasBreak;
        // Disabled break always stores NULL, even if the client submitted stale form values.
        schedule.BreakStart = request.HasBreak ? request.BreakStart : null;
        schedule.BreakEnd = request.HasBreak ? request.BreakEnd : null;
        schedule.IsActive = request.IsActive;
    }
    private async Task ValidateSchedule(DoctorSchedule schedule)
    {
        if (schedule.DayOfWeek is < 1 or > 7 || schedule.StartTime >= schedule.EndTime)
            throw new ApiException(400, "Día u horas inválidos; inicio debe ser anterior al fin.");
        if (schedule.HasBreak && (schedule.BreakStart is null || schedule.BreakEnd is null
            || schedule.StartTime >= schedule.BreakStart || schedule.BreakStart >= schedule.BreakEnd || schedule.BreakEnd >= schedule.EndTime))
            throw new ApiException(400, "El descanso debe estar estrictamente dentro del bloque.");
        if (!await db.DoctorCenters.AnyAsync(x => x.DoctorId == schedule.DoctorId && x.CenterId == schedule.CenterId
            && x.IsActive && x.Doctor.IsActive && x.Center.IsActive))
            throw new ApiException(400, "El doctor debe estar activo y asignado a una sucursal activa.");
        // Half-open intervals allow adjacent blocks. Breaks remain reserved for this doctor.
        if (schedule.IsActive && await db.DoctorSchedules.AnyAsync(x => x.Id != schedule.Id && x.DoctorId == schedule.DoctorId
            && x.IsActive && x.DayOfWeek == schedule.DayOfWeek && x.StartTime < schedule.EndTime && schedule.StartTime < x.EndTime))
            throw new ApiException(409, "El horario se solapa con otro bloque del doctor, incluso en otra sucursal.");
    }
    private async Task DisableSchedules(IQueryable<DoctorSchedule> query, string reason)
    {
        foreach (var schedule in await query.Where(x => x.IsActive).ToArrayAsync())
        {
            schedule.IsActive = false;
            support.Audit("schedule_status_changed", "DoctorSchedule", schedule.Id,
                metadata: new { doctorId = schedule.DoctorId, centerId = schedule.CenterId, isActive = false, reason });
        }
    }
    private async Task<DoctorScheduleResponse> Schedule(Guid id) => ScheduleDto(await db.DoctorSchedules.AsNoTracking().Include(x => x.Center).SingleAsync(x => x.Id == id));
    public async Task<DashboardResponse> Dashboard()
    {
        var actor = support.Actor ?? throw new ApiException(401, "Sesión requerida.");
        var permissions = (await support.Current(actor)).Permissions;
        if (!permissions.Intersect(new[] { "users.view", "doctors.view", "centers.view", "specialties.view" }).Any())
            throw new ApiException(403, "No tiene permisos para consultar el resumen administrativo.");
        return new(permissions.Contains("users.view") ? await db.Users.CountAsync() : null,
            permissions.Contains("doctors.view") ? await db.DoctorProfiles.CountAsync() : null,
            permissions.Contains("doctors.view") ? await db.DoctorProfiles.CountAsync(x => x.IsActive) : null,
            permissions.Contains("centers.view") ? await db.Centers.CountAsync() : null,
            permissions.Contains("centers.view") ? await db.Centers.CountAsync(x => x.IsActive) : null,
            permissions.Contains("specialties.view") ? await db.Specialties.CountAsync() : null);
    }
    private static ClinicResponse ClinicDto(Clinic x) => new(x.Id, x.Name, x.LegalName, x.Description, x.LogoUrl,
        x.Phone, x.Whatsapp, x.Email, x.Website, x.TaxId, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static CenterResponse CenterDto(Center x) => new(x.Id, x.ClinicId, x.Name, x.Code, x.Description, x.Address,
        x.City, x.Department, x.Country, x.Phone, x.Whatsapp, x.Email, x.GoogleMapsUrl, x.Latitude, x.Longitude,
        x.IsMain, x.IsActive, x.CreatedAt, x.UpdatedAt);
    private static SpecialtyResponse SpecialtyDto(Specialty x) => new(x.Id, x.Code, x.Name, x.Description, x.IsActive,
        x.DoctorSpecialties.Count, x.CreatedAt, x.UpdatedAt);
    private static DoctorResponse DoctorDto(DoctorProfile x) => new(x.Id, x.UserId,
        new(x.User.Id, x.User.FirstName, x.User.LastName, x.User.Email, x.User.IsActive), x.ProfessionalLicense, x.Ci,
        x.BirthDate, x.Gender, x.Phone, x.Address, x.PhotoUrl, x.Bio, x.IsActive, x.CreatedAt, x.UpdatedAt,
        x.DoctorSpecialties.OrderBy(a => a.Specialty.Name).Select(a => new AssignmentResponse(a.SpecialtyId, a.Specialty.Code, a.Specialty.Name, a.Specialty.IsActive)).ToArray(),
        x.DoctorCenters.Where(a => a.IsActive).OrderBy(a => a.Center.Name).Select(a => new AssignmentResponse(a.CenterId, a.Center.Code, a.Center.Name, a.Center.IsActive)).ToArray());
    private static DoctorScheduleResponse ScheduleDto(DoctorSchedule x) => new(x.Id, x.DoctorId, x.CenterId, x.Center.Name,
        x.DayOfWeek, x.StartTime, x.EndTime, x.HasBreak, x.BreakStart, x.BreakEnd, x.IsActive, x.CreatedAt, x.UpdatedAt);
}
