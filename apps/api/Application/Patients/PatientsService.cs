using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.Patients;

public sealed class PatientsService(AppDbContext db, IdentitySupport support)
{
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async Task Mutate(Func<Task> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        await action();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    private async Task<PatientProfile> Find(Guid id) => await db.PatientProfiles.SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiException(404, "Paciente inexistente.");
    public async Task<PatientListResponse> List(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ApiException(400, "Paginación inválida.");
        var query = db.PatientProfiles.AsNoTracking();
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.LastName).ThenBy(x => x.FirstName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return new(items.Select(Dto).ToArray(), total, page, pageSize);
    }
    public async Task<PatientResponse> Get(Guid id) => Dto(await db.PatientProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiException(404, "Paciente inexistente."));
    public async Task<PatientResponse> Create(PatientCreateRequest request)
    {
        var patient = new PatientProfile();
        await Mutate(async () => {
            await Apply(patient, request);
            db.PatientProfiles.Add(patient);
            support.Audit("patient_created", "PatientProfile", patient.Id);
        });
        return await Get(patient.Id);
    }
    public async Task<PatientResponse> Update(Guid id, PatientUpdateRequest request)
    {
        await Mutate(async () => {
            var patient = await Find(id);
            var changed = patient.IsActive != request.IsActive;
            if (changed && !await db.UserRoles.AnyAsync(x => x.UserId == support.Actor && x.User.IsActive && x.Role.IsActive
                && x.Role.RolePermissions.Any(p => p.Permission.Code == "patients.disable")))
                throw new ApiException(403, "No tiene permiso para cambiar el estado.");
            await Apply(patient, request);
            support.Audit("patient_updated", "PatientProfile", id);
            if (changed) support.Audit("patient_status_changed", "PatientProfile", id, metadata: new { isActive = patient.IsActive });
        });
        return await Get(id);
    }
    public Task Status(Guid id, bool active) => Mutate(async () => {
        var patient = await Find(id);
        patient.IsActive = active;
        support.Audit("patient_status_changed", "PatientProfile", id, metadata: new { isActive = active });
    });
    private async Task Apply(PatientProfile patient, PatientUpdateRequest request)
    {
        var ci = Optional(request.Ci)?.ToUpperInvariant();
        if (ci != null && await db.PatientProfiles.AnyAsync(x => x.Id != patient.Id && x.Ci == ci))
            throw new ApiException(409, "CI ya registrado.");
        if (request.UserId is Guid userId)
        {
            if (!await db.Users.AnyAsync(x => x.Id == userId)) throw new ApiException(404, "Usuario inexistente.");
            if (await db.PatientProfiles.AnyAsync(x => x.Id != patient.Id && x.UserId == userId))
                throw new ApiException(409, "Usuario ya vinculado a un paciente.");
            if (!await db.UserRoles.AnyAsync(x => x.UserId == userId && x.Role.Code == "PATIENT" && x.Role.IsActive))
                throw new ApiException(400, "El usuario debe tener el rol PATIENT activo.");
        }
        if (patient.UserId != request.UserId)
            support.Audit("patient_user_linked", "PatientProfile", patient.Id, metadata: new { previousUserId = patient.UserId, userId = request.UserId });
        patient.UserId = request.UserId;
        patient.FirstName = Optional(request.FirstName) ?? throw new ApiException(400, "Nombre obligatorio.");
        patient.LastName = Optional(request.LastName) ?? throw new ApiException(400, "Apellido obligatorio.");
        patient.Ci = ci; patient.BirthDate = request.BirthDate; patient.Gender = Optional(request.Gender);
        patient.Phone = Optional(request.Phone); patient.Email = Optional(request.Email)?.ToLowerInvariant();
        patient.Address = Optional(request.Address); patient.EmergencyContactName = Optional(request.EmergencyContactName);
        patient.EmergencyContactPhone = Optional(request.EmergencyContactPhone); patient.BloodType = Optional(request.BloodType);
        patient.Notes = Optional(request.Notes); patient.IsActive = request.IsActive;
    }
    private static PatientResponse Dto(PatientProfile p) => new(p.Id, p.UserId, p.FirstName, p.LastName, p.Ci,
        p.BirthDate, p.Gender, p.Phone, p.Email, p.Address, p.EmergencyContactName, p.EmergencyContactPhone,
        p.BloodType, p.Notes, p.IsActive, p.CreatedAt, p.UpdatedAt);
}
