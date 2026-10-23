using System.ComponentModel.DataAnnotations;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Persistence;

public sealed class IdentitySeed(AppDbContext db, IdentitySupport support, IPasswordHasher<User> hasher,
    IConfiguration config, ILogger<IdentitySeed> logger)
{
    public static readonly string[] PermissionCodes = ["users.view", "users.create", "users.update", "users.disable", "roles.view", "roles.create", "roles.update", "roles.assign_permissions", "permissions.view", "clinic.view", "clinic.update", "centers.view", "centers.create", "centers.update", "centers.disable", "doctors.view", "doctors.create", "doctors.update", "doctors.disable", "specialties.view", "specialties.create", "specialties.update", "specialties.disable", "schedules.view", "schedules.create", "schedules.update", "schedules.disable", "patients.view", "patients.create", "patients.update", "patients.disable", "appointments.view", "appointments.create", "appointments.update", "appointments.cancel", "clinical_history.view", "clinical_history.create", "clinical_history.update", "odontogram.view", "odontogram.create", "odontogram.update", "periodontogram.view", "periodontogram.create", "periodontogram.update", "treatments.view", "treatments.create", "treatments.update", "prescriptions.view", "prescriptions.create", "inventory.view", "inventory.create", "inventory.update", "inventory.item.manage", "inventory.movement.view", "inventory.entry.create", "inventory.withdrawal.create", "inventory.return.create", "inventory.adjustment.create", "inventory.doctor.withdraw", "payments.view", "payments.create", "reports.view", "audit.view"];
    public async Task Run()
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        var catalog = await db.Permissions.ToListAsync();
        foreach (var code in PermissionCodes.Where(code => catalog.All(x => x.Code != code)))
        {
            var permission = new Permission { Code = code, Name = code, Module = code.Split('.')[0] };
            db.Permissions.Add(permission); catalog.Add(permission);
        }
        var names = new Dictionary<string, string> {
            ["ADMINISTRATOR"] = "Administrador", ["SECRETARY"] = "Secretaria",
            ["DOCTOR"] = "Doctor", ["PATIENT"] = "Paciente",
            ["CASHIER"] = "Cajero/a", ["DENTAL_ASSISTANT"] = "Asistente dental"
        };
        var roles = await db.Roles.Include(x => x.RolePermissions).ToListAsync();
        foreach (var (code, name) in names)
        {
            var role = roles.SingleOrDefault(x => x.Code == code);
            var created = role is null;
            if (role is null) { role = new Role { Code = code, Name = name, IsSystem = true }; db.Roles.Add(role); roles.Add(role); }
            // No reponer permisos quitados por un administrador a roles existentes.
            if (created || code == "ADMINISTRATOR")
                foreach (var permission in catalog.Where(p => Grants(code, p.Code)))
                    if (role.RolePermissions.All(x => x.PermissionId != permission.Id))
                        role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }
        await db.SaveChangesAsync();
        if (!await db.Users.AnyAsync())
        {
            var email = config["BOOTSTRAP_ADMIN_EMAIL"]?.Trim().ToLowerInvariant();
            var password = config["BOOTSTRAP_ADMIN_PASSWORD"];
            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email) || email.Length > 254
                || string.IsNullOrWhiteSpace(password) || password == "CHANGE_ME" || password.Length is < 12 or > 128)
                logger.LogWarning("Bootstrap omitido: configure email válido y contraseña segura (12-128 caracteres).");
            else
            {
                var user = new User { Email = email, FirstName = "Administrador", LastName = "Inicial" };
                user.PasswordHash = hasher.HashPassword(user, password);
                db.Users.Add(user);
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roles.Single(x => x.Code == "ADMINISTRATOR").Id });
                support.Audit("user_created", "User", user.Id);
                support.Audit("user_roles_updated", "User", user.Id);
                await db.SaveChangesAsync();
            }
        }
        await tx.CommitAsync();
    }
    private static bool Grants(string role, string permission) => role switch
    {
        "ADMINISTRATOR" => true,
        "SECRETARY" => permission is "patients.view" or "patients.create" or "patients.update"
            or "payments.view" or "payments.create" || permission.StartsWith("appointments."),
        "DOCTOR" => permission is "inventory.view" or "inventory.doctor.withdraw" || permission == "patients.view" || new[] {"clinical_history.", "odontogram.", "periodontogram.", "treatments.", "prescriptions."}.Any(permission.StartsWith),
        "CASHIER" => permission is "payments.view" or "payments.create",
        "DENTAL_ASSISTANT" => permission is "patients.view" or "appointments.view" or "inventory.view",
        _ => false
    };
}
