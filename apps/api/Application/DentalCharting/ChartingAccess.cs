using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.Domain.DentalCharting;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
namespace ClinicaRamirez.Api.Application.DentalCharting;

public sealed class ChartingAccess(AppDbContext db, IdentitySupport support)
{
    public async Task<Guid> Doctor() {
        var actor = support.Actor ?? throw new ApiException(401, "Inicia sesión.");
        if (!await db.UserRoles.AnyAsync(x => x.UserId == actor && x.User.IsActive && x.Role.IsActive && x.Role.Code == "DOCTOR"))
            throw new ApiException(403, "Se requiere el rol DOCTOR activo.");
        return await db.DoctorProfiles.Where(x => x.UserId == actor && x.IsActive).Select(x => (Guid?)x.Id).SingleOrDefaultAsync()
            ?? throw new ApiException(403, "No hay perfil profesional activo.");
    }
    public async Task<Guid> Patient(Guid patient) {
        var doctor = await Doctor();
        if (!await db.Appointments.AnyAsync(x => x.PatientId == patient && x.DoctorId == doctor)) throw new ApiException(404, "Paciente no disponible para tu atención.");
        return doctor;
    }
    public async Task Encounter(Guid? encounter, Guid patient, Guid doctor) {
        if (encounter.HasValue && !await db.Set<ClinicalEncounter>().AnyAsync(x => x.Id == encounter && x.PatientProfileId == patient && x.DoctorProfileId == doctor))
            throw new ApiException(404, "Consulta no disponible para este paciente y doctor.");
    }
    public async Task<T> Write<T>(Func<Task<T>> action) {
        await using var tx = await db.Database.BeginTransactionAsync();
        // Same transaction-scoped clinical lock used by clinical encounters/SOAP. No distributed infrastructure.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(820030)");
        try { var result = await action(); await db.SaveChangesAsync(); await tx.CommitAsync(); return result; }
        catch (DbUpdateConcurrencyException) { throw new ApiException(409, "El examen cambió. Recarga antes de guardar."); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" or "23503" or "23514" }) {
            throw new ApiException(409, "El examen cambió o sus relaciones no permiten esta operación.");
        }
    }
    public static void Mode(string mode) { if (!ChartingRules.Dentitions.Contains(mode)) throw new ApiException(400, "Dentición inválida."); }
    public async Task ValidateTeeth(IEnumerable<int> ids, string mode) {
        Mode(mode); var requested = ids.Distinct().ToArray();
        var valid = await db.Set<DentalTooth>().Where(x => requested.Contains(x.Id) && x.IsActive && (mode == "MIXED" || x.DentitionClass == mode)).CountAsync();
        if (valid != requested.Length) throw new ApiException(400, "Diente inexistente, inactivo o incompatible con la dentición.");
    }
    public static void Text(string? value) { if (value?.Length > ChartingRules.MaxNotes) throw new ApiException(400, "Observación demasiado extensa."); }
    public void Audit(string action, IChartExam e) => support.Audit(action, e is OdontogramExam ? "OdontogramExam" : "PeriodontalExam", e.Id,
        metadata: new { ExamId=e.Id, e.PatientProfileId, e.ClinicalEncounterId, e.PreviousExamId, e.DentitionMode });
}
