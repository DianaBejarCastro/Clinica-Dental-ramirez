using System.ComponentModel.DataAnnotations;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace ClinicaRamirez.Api.Application.Deletion;
public sealed record DeletionDependency(string Type, long Count);
public sealed record DeletionCheck(bool CanDelete, string Message, DeletionDependency[] Dependencies, bool Protected = false);
public sealed record ProtectedDeleteRequest([Required, MaxLength(128)] string Password);

// Default deny. Toda entidad futura requiere entrada explícita, estado inactivo,
// autorización ADMINISTRATOR, evento de auditoría y FK RESTRICT (ver docs/architecture/protected-deletion.md).
public sealed class DeletionGuardService(AppDbContext db, IdentitySupport support, IPasswordHasher<User> hasher)
{
    private sealed record Rule(Type Type, string Event);
    private static readonly IReadOnlyDictionary<string, Rule> Rules = new Dictionary<string, Rule> {
        ["centers"] = new(typeof(Center), "center_deleted"), ["specialties"] = new(typeof(Specialty), "specialty_deleted"),
        ["doctors"] = new(typeof(DoctorProfile), "doctor_deleted"), ["schedules"] = new(typeof(DoctorSchedule), "schedule_deleted"),
        ["patients"] = new(typeof(PatientProfile), "patient_deleted"), ["appointments"] = new(typeof(Appointment), "appointment_deleted"),
        ["social-networks"] = new(typeof(ClinicSocialNetwork), "clinic_social_network_deleted"),
        ["users"] = new(typeof(User), "user_deleted"), ["roles"] = new(typeof(Role), "role_deleted")
    };
    private static DeletionCheck Block(string message, bool protectedEntity = false) => new(false, message, [], protectedEntity);
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private async Task<User> Administrator(CancellationToken ct)
    {
        var id = support.Actor ?? throw new ApiException(401, "Inicia sesión para continuar.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (user is null || !await db.UserRoles.AnyAsync(x => x.UserId == id && x.Role.Code == "ADMINISTRATOR" && x.Role.IsActive, ct))
            throw new ApiException(403, "Solo un administrador puede eliminar definitivamente.");
        return user;
    }
    private async Task<DeletionCheck> Inspect(Rule rule, object entity, Guid id, CancellationToken ct)
    {
        if (entity is User user) {
            if (id == support.Actor) return Block("No puedes eliminar tu propia cuenta.", true);
            if (user.IsActive && await db.UserRoles.AnyAsync(x => x.UserId == id && x.Role.Code == "ADMINISTRATOR" && x.Role.IsActive, ct)
                && !await db.UserRoles.AnyAsync(x => x.UserId != id && x.User.IsActive && x.Role.Code == "ADMINISTRATOR" && x.Role.IsActive, ct))
                return Block("No se puede eliminar el último administrador activo.", true);
        }
        if (entity is Role { IsSystem: true }) return Block("Los roles del sistema están protegidos.", true);
        if (entity is Specialty specialty) {
            var origins = await db.AuditLogs.Where(x => x.EntityId == id && x.Entity == "Specialty" && x.Action == "specialty_created")
                .Select(x => x.MetadataJson).ToArrayAsync(ct);
            if (ClinicSeed.InitialSpecialties.Any(x => x.Code == specialty.Code) || origins.Any(x => x?.Contains("\"seed\"") == true))
                return Block("Las especialidades iniciales del sistema están protegidas.", true);
        }
        var dependencies = await Dependencies(rule.Type, id, ct);
        if (dependencies.Length > 0) return new(false, "Este registro tiene información vinculada y no puede eliminarse.", dependencies);
        var inactive = entity switch {
            Center x => !x.IsActive, Specialty x => !x.IsActive, DoctorProfile x => !x.IsActive,
            DoctorSchedule x => !x.IsActive, PatientProfile x => !x.IsActive, ClinicSocialNetwork x => !x.IsActive,
            User x => !x.IsActive, Role x => !x.IsActive,
            Appointment x => x.Status is "CANCELLED" or "NO_SHOW", _ => false
        };
        return inactive ? new(true, "Esta acción no se puede deshacer.", []) : Block("Desactiva el registro antes de eliminarlo. Las citas deben estar canceladas o marcadas como No asistió.");
    }
    private async Task<DeletionDependency[]> Dependencies(Type type, Guid id, CancellationToken ct)
    {
        var metadata = db.Model.FindEntityType(type)!; var table = metadata.GetTableName()!; var schema = metadata.GetSchema() ?? "public";
        await db.Database.OpenConnectionAsync(ct);
        using var command = db.Database.GetDbConnection().CreateCommand(); command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT ns.nspname, t.relname, a.attname, c.confdeltype::text, cardinality(c.conkey)
            FROM pg_constraint c JOIN pg_class t ON t.oid=c.conrelid JOIN pg_namespace ns ON ns.oid=t.relnamespace
            JOIN pg_class p ON p.oid=c.confrelid JOIN pg_namespace pn ON pn.oid=p.relnamespace
            JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=c.conkey[1]
            WHERE c.contype='f' AND pn.nspname=@schema AND p.relname=@table
            """;
        command.Parameters.Add(new NpgsqlParameter("schema", schema)); command.Parameters.Add(new NpgsqlParameter("table", table));
        var refs = new List<(string Schema, string Table, string Column)>();
        await using (var reader = await command.ExecuteReaderAsync(ct)) {
            while (await reader.ReadAsync(ct)) {
                // Fail closed if a future schema introduces cascade/SET NULL/composite references.
                if (reader.GetString(3) != "r" || reader.GetInt32(4) != 1) return [new("Relación estructural no compatible con eliminación segura", 1)];
                refs.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }
        }
        var result = new List<DeletionDependency>();
        foreach (var group in refs.GroupBy(x => (x.Schema, x.Table))) {
            using var count = db.Database.GetDbConnection().CreateCommand(); count.Transaction = command.Transaction;
            count.CommandText = $"SELECT count(*) FROM {Quote(group.Key.Schema)}.{Quote(group.Key.Table)} WHERE " + string.Join(" OR ", group.Select(x => $"{Quote(x.Column)} = @id"));
            count.Parameters.Add(new NpgsqlParameter("id", id)); var value = Convert.ToInt64(await count.ExecuteScalarAsync(ct));
            if (value > 0) result.Add(new(group.Key.Table, value));
        }
        return result.OrderBy(x => x.Type).ToArray();
    }
    public async Task<DeletionCheck> Check(string kind, Guid id, CancellationToken ct)
    {
        await Administrator(ct);
        if (!Rules.TryGetValue(kind, out var rule)) return Block("Esta categoría está protegida y no permite eliminación definitiva.", true);
        var entity = await db.FindAsync(rule.Type, [id], ct) ?? throw new ApiException(404, "Registro inexistente.");
        return await Inspect(rule, entity, id, ct);
    }
    public async Task<DeletionCheck> Delete(string kind, Guid id, string password, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await support.LockAdministration(); var actor = await Administrator(ct);
        if (!Rules.TryGetValue(kind, out var rule)) return Block("Esta categoría está protegida y no permite eliminación definitiva.", true);
        if (string.IsNullOrEmpty(password) || hasher.VerifyHashedPassword(actor, actor.PasswordHash, password) == PasswordVerificationResult.Failed)
            throw new ApiException(403, "Contraseña incorrecta.");
        var model = db.Model.FindEntityType(rule.Type)!;
        var table = Quote(model.GetSchema() ?? "public") + "." + Quote(model.GetTableName()!);
        // FOR UPDATE serializa cambios del registro y las inserciones de FK concurrentes.
        var lockSql = $"SELECT \"Id\" FROM {table} WHERE \"Id\" = @id FOR UPDATE";
        await db.Database.ExecuteSqlRawAsync(lockSql, [new NpgsqlParameter("id", id)], ct);
        var entity = await db.FindAsync(rule.Type, [id], ct) ?? throw new ApiException(404, "Registro inexistente.");
        await db.Entry(entity).ReloadAsync(ct);
        var check = await Inspect(rule, entity, id, ct); if (!check.CanDelete) return check;
        try {
            // Única excepción controlada a los guards de SaveChanges: SQL de tabla allowlisted,
            // id parametrizado, transacción y auditoría. Los guards habituales permanecen intactos.
            var deleteSql = $"DELETE FROM {table} WHERE \"Id\" = @id";
            var deleted = await db.Database.ExecuteSqlRawAsync(deleteSql, [new NpgsqlParameter("id", id)], ct);
            if (deleted != 1) throw new ApiException(404, "Registro inexistente.");
            // The domain grant cannot survive its profile. Preserve identity and every other role.
            var domain = entity switch {
                DoctorProfile doctor => (UserId: doctor.UserId, Code: "DOCTOR"),
                PatientProfile patient => (UserId: patient.UserId, Code: "PATIENT"),
                _ => (UserId: Guid.Empty, Code: "")
            };
            if (domain.UserId != Guid.Empty) {
                var grant = await db.UserRoles.SingleOrDefaultAsync(x => x.UserId == domain.UserId && x.Role.Code == domain.Code, ct);
                if (grant is not null) {
                    db.UserRoles.Remove(grant);
                    support.Audit("user_role_removed", "User", domain.UserId, metadata: new { roleCode = domain.Code, reason = "domain_profile_deleted" });
                }
            }
            db.Entry(entity).State = EntityState.Detached;
            support.Audit(rule.Event, rule.Type.Name, id, metadata: new { entityId = id, entityType = rule.Type.Name, deletedAt = DateTime.UtcNow });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(true, "Registro eliminado definitivamente.", []);
        }
        catch (Exception ex) when (ex is PostgresException { SqlState: "23503" or "23001" } || ex is DbUpdateException { InnerException: PostgresException { SqlState: "23503" or "23001" } }) {
            await tx.RollbackAsync(ct);
            return new(false, "Apareció información vinculada. No se eliminó el registro.", [new("Referencia concurrente", 1)]);
        }
    }
}
