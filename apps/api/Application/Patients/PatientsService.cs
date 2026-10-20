using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.Patients;

public sealed class PatientsService(AppDbContext db, IdentitySupport support, PatientAccountService accounts)
{
    public async Task<PatientSearchResult[]> Search(string? q, int limit = 10, CancellationToken ct = default)
    {
        if (q?.Length > 200 || limit < 1) throw new ApiException(400, "Búsqueda o límite inválido.");
        var term = string.Join(' ', (q ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
        if (term.Length < 2) return [];
        var query = db.PatientProfiles.AsNoTracking().Where(p => p.IsActive);
        foreach (var word in term.Split(' ')) {
            var token = word;
            query = query.Where(p => (p.User.Ci != null && p.User.Ci.ToLower().Contains(token))
                || (p.User.FirstName + " " + p.User.LastName).ToLower().Contains(token)
                || (p.User.Email != null && p.User.Email.ToLower().Contains(token))
                || (p.User.Phone != null && p.User.Phone.Contains(token)));
        }
        // Bounded database query; no additional indexes/extensions for the current volume.
        // Leading-wildcard searches may warrant pg_trgm only after measuring production plans.
        return await query.OrderBy(p => p.User.Ci != null && p.User.Ci.ToLower() == term ? 0
            : p.User.Ci != null && p.User.Ci.ToLower().StartsWith(term) ? 1
            : p.User.FirstName.ToLower().StartsWith(term) || p.User.LastName.ToLower().StartsWith(term) ? 2 : 3)
            .ThenBy(p => p.User.LastName).ThenBy(p => p.User.FirstName).ThenBy(p => p.Id)
            .Take(Math.Min(limit, 20))
            .Select(p => new PatientSearchResult(p.Id, p.UserId, p.User.FirstName, p.User.LastName, p.User.Ci, p.User.Phone, p.User.Email))
            .ToArrayAsync(ct);
    }
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private async Task Mutate(Func<Task> action)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        await action();
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    private async Task<PatientProfile> Find(Guid id) => await db.PatientProfiles.Include(x => x.User).SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiException(404, "Paciente inexistente.");
    public async Task<PatientListResponse> List(int page, int pageSize)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new ApiException(400, "Paginación inválida.");
        var query = db.PatientProfiles.Include(x => x.User).AsNoTracking();
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.User.LastName).ThenBy(x => x.User.FirstName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return new(items.Select(Dto).ToArray(), total, page, pageSize);
    }
    public async Task<PatientResponse> Get(Guid id) => Dto(await db.PatientProfiles.Include(x => x.User).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id)
        ?? throw new ApiException(404, "Paciente inexistente."));
    public async Task<PatientResponse> Create(PatientCreateRequest request, bool selfRegistration = false)
    {
        var user = new User { CanLogin = false };
        var patient = new PatientProfile { User = user, UserId = user.Id };
        await Mutate(async () => {
            await Apply(patient, request);
            await accounts.AssignPatientRole(user);
            if (request.Account != null) await accounts.Create(patient, request.Account);
            db.PatientProfiles.Add(patient);
            if (selfRegistration) support.Audit("patient_self_registered", "PatientProfile", patient.Id);
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
    public async Task<PatientResponse> CreateAccount(Guid id, PatientAccountCreateRequest request)
    {
        await Mutate(async () => { await accounts.Create(await Find(id), request); });
        return await Get(id);
    }
    public Task Status(Guid id, bool active) => Mutate(async () => {
        var patient = await Find(id);
        patient.IsActive = active;
        support.Audit("patient_status_changed", "PatientProfile", id, metadata: new { isActive = active });
    });
    private async Task Apply(PatientProfile patient, PatientUpdateRequest request)
    {
        PatientAccountService.Validate(request);
        var ci = request.Ci!.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.Id != patient.UserId && x.Ci == ci))
            throw new ApiException(409, "CI ya registrado.");
        patient.User.FirstName = Optional(request.FirstName) ?? throw new ApiException(400, "Nombre obligatorio.");
        patient.User.LastName = Optional(request.LastName) ?? throw new ApiException(400, "Apellido obligatorio.");
        patient.User.Ci = ci; patient.User.BirthDate = request.BirthDate!.Value; patient.User.Gender = Optional(request.Gender);
        patient.User.Phone = request.Phone!.Trim();
        patient.User.Address = Optional(request.Address); patient.EmergencyContactName = request.EmergencyContactName!.Trim();
        patient.EmergencyContactPhone = request.EmergencyContactPhone!.Trim(); patient.BloodType = request.BloodType!;
        patient.Notes = Optional(request.Notes); patient.IsActive = request.IsActive;
    }
    private static PatientResponse Dto(PatientProfile p) => new(p.Id, p.UserId, p.User.FirstName, p.User.LastName, p.User.Ci,
        p.User.BirthDate, p.User.Gender, p.User.Phone, p.User.Email, p.User.Address, p.EmergencyContactName, p.EmergencyContactPhone,
        p.BloodType, p.Notes, p.IsActive, p.CreatedAt, p.UpdatedAt, p.User.CanLogin);
}
