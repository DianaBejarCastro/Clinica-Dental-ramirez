using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ClinicaRamirez.Api.Application.Patients;
public sealed class PatientRegistrationService(AppDbContext db, IdentitySupport support, IPasswordHasher<User> hasher)
{
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    public async Task Register(PatientRegistrationRequest request, CancellationToken ct)
    {
        var first = Optional(request.FirstName) ?? throw new ApiException(400, "Nombre obligatorio.");
        var last = Optional(request.LastName) ?? throw new ApiException(400, "Apellido obligatorio.");
        var email = request.Email.Trim().ToLowerInvariant(); var ci = Optional(request.Ci)?.ToUpperInvariant();
        if (request.Password.Length is < 12 or > 128) throw new ApiException(400, "La contraseña debe tener entre 12 y 128 caracteres.");
        if (request.Password != request.ConfirmPassword) throw new ApiException(400, "Las contraseñas no coinciden.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await support.LockAdministration();
        if (await db.Users.AnyAsync(x => x.Email == email, ct)) throw new ApiException(409, "Ya existe una cuenta registrada con este correo electrónico.");
        if (ci != null && await db.PatientProfiles.AnyAsync(x => x.Ci == ci, ct)) throw new ApiException(409, "CI ya registrado.");
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Code == "PATIENT" && x.IsActive, ct)
            ?? throw new ApiException(503, "El registro de pacientes no está disponible en este momento.");
        var user = new User { FirstName = first, LastName = last, Email = email, Phone = Optional(request.Phone), IsActive = true };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { User = user, RoleId = role.Id });
        try
        {
            await db.SaveChangesAsync(ct);
            var patient = new PatientProfile { UserId = user.Id, FirstName = first, LastName = last, Email = email,
                Ci = ci, BirthDate = request.BirthDate, Gender = Optional(request.Gender), Phone = user.Phone, IsActive = true };
            db.PatientProfiles.Add(patient);
            support.Audit("patient_self_registered", "PatientProfile", patient.Id);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        { throw new ApiException(409, pg.ConstraintName == "IX_Users_Email" ? "Ya existe una cuenta registrada con este correo electrónico." : "CI ya registrado."); }
    }
}
