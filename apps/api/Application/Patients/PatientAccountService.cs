using System.ComponentModel.DataAnnotations;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.Patients;

public sealed class PatientAccountService(AppDbContext db, IdentitySupport support, IPasswordHasher<User> hasher)
{
    public static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), errors, true))
            throw new ApiException(400, errors[0].ErrorMessage ?? "Datos inválidos.");
    }

    // Called only inside the patient's transaction and administrative lock.
    public async Task Create(PatientProfile patient, PatientAccountCreateRequest request)
    {
        Validate(request);
        if (patient.User.CanLogin) throw new ApiException(409, "El paciente ya tiene una cuenta vinculada.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email)) throw new ApiException(409, "Ya existe una cuenta registrada con este correo electrónico.");
        var user = patient.User;
        user.Email = email;
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        user.CanLogin = true;
        support.Audit("patient_account_enabled", "PatientProfile", patient.Id);
    }

    public async Task AssignPatientRole(User user)
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Code == "PATIENT" && x.IsActive)
            ?? throw new ApiException(503, "La creación de pacientes no está disponible.");
        db.UserRoles.Add(new UserRole { User = user, RoleId = role.Id, AssignedByUserId = support.Actor });
    }
}
