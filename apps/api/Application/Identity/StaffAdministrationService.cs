using System.ComponentModel.DataAnnotations;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.DentalAssistants;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.DTOs;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.Identity;

public sealed class StaffAdministrationService(AppDbContext db, IdentitySupport support, IPasswordHasher<User> hasher)
{
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Validate(object request)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new(request), errors, true)) throw new ApiException(400, errors[0].ErrorMessage!);
    }
    private async Task<Role> Role(string code) => await db.Roles.SingleOrDefaultAsync(x => x.Code == code && x.IsActive)
        ?? throw new ApiException(409, "El rol requerido no está disponible.");
    private async Task<UserResponse> Response(Guid id) => (await support.UsersDto([await db.Users.AsNoTracking()
        .Include(x => x.UserRoles).ThenInclude(x => x.Role).SingleAsync(x => x.Id == id)])).Single();
    public async Task<UserResponse> Create(string code, StaffCreateRequest request)
    {
        if (code is not ("ADMINISTRATOR" or "CASHIER" or "SECRETARY")) throw new ApiException(400, "Rol no permitido.");
        Validate(request);
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Email == email)) throw new ApiException(409, "El email ya se encuentra registrado.");
        var role = await Role(code);
        var user = new User { FirstName=request.FirstName.Trim(), LastName=request.LastName.Trim(),
            BirthDate=request.BirthDate, Gender=Optional(request.Gender), Phone=request.Phone.Trim(), Address=Optional(request.Address),
            Email=email, CanLogin=true, IsActive=true };
        await IdentityDocument.Apply(db, user, request.DocumentType, request.DocumentNumber, request.DocumentCountryCode);
        user.PasswordHash=hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole { User=user, RoleId=role.Id, AssignedByUserId=support.Actor });
        support.Audit(code.ToLowerInvariant()+"_created", "User", user.Id);
        support.Audit("user_role_added", "User", user.Id, metadata: new { roleCode=code });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return await Response(user.Id);
    }
    // Adding a domain role never replaces identity, credentials, or existing roles.
    public async Task<UserResponse> AddProfile(Guid userId, string code, object request)
    {
        Validate(request);
        await using var tx=await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        var user=await db.Users.SingleOrDefaultAsync(x=>x.Id==userId) ?? throw new ApiException(404,"Usuario inexistente.");
        if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName) || string.IsNullOrWhiteSpace(user.DocumentNumber)
            || user.BirthDate is null || user.BirthDate > LocalBirthDateAttribute.Today || string.IsNullOrWhiteSpace(user.Phone))
            throw new ApiException(409,"Completa los datos personales del usuario antes de añadir este rol.");
        if (code is "DOCTOR" or "DENTAL_ASSISTANT" && (!user.CanLogin || string.IsNullOrWhiteSpace(user.Email) || string.IsNullOrWhiteSpace(user.PasswordHash)))
            throw new ApiException(409,"El usuario debe tener una cuenta de acceso habilitada antes de añadir este rol.");
        var role=await Role(code);
        var hasRole=await db.UserRoles.AnyAsync(x=>x.UserId==userId && x.RoleId==role.Id);
        Guid profileId; string entity; string audit;
        switch(request)
        {
            case AddDoctorProfileRequest doctor when code=="DOCTOR":
                if (await db.DoctorProfiles.AnyAsync(x=>x.UserId==userId)) throw new ApiException(409,"El usuario ya tiene un perfil de doctor.");
                var license=doctor.ProfessionalLicense.Trim().ToUpperInvariant();
                if (await db.DoctorProfiles.AnyAsync(x=>x.ProfessionalLicense==license)) throw new ApiException(409,"La matrícula profesional ya existe.");
                var d=new DoctorProfile { UserId=userId, ProfessionalLicense=license, Bio=Optional(doctor.Bio), IsActive=user.IsActive };
                db.DoctorProfiles.Add(d); profileId=d.Id; entity="DoctorProfile"; audit="doctor_created"; break;
            case AddPatientProfileRequest patient when code=="PATIENT":
                if (await db.PatientProfiles.AnyAsync(x=>x.UserId==userId)) throw new ApiException(409,"El usuario ya tiene un perfil de paciente.");
                var p=new PatientProfile { UserId=userId, EmergencyContactName=patient.EmergencyContactName.Trim(), EmergencyContactPhone=patient.EmergencyContactPhone.Trim(), BloodType=patient.BloodType, Notes=Optional(patient.Notes), IsActive=user.IsActive };
                db.PatientProfiles.Add(p); profileId=p.Id; entity="PatientProfile"; audit="patient_created"; break;
            case AddDentalAssistantProfileRequest when code=="DENTAL_ASSISTANT":
                if (await db.DentalAssistantProfiles.AnyAsync(x=>x.UserId==userId)) throw new ApiException(409,"El usuario ya tiene un perfil de asistente dental.");
                var a=new DentalAssistantProfile { UserId=userId, IsActive=user.IsActive };
                db.DentalAssistantProfiles.Add(a); profileId=a.Id; entity="DentalAssistantProfile"; audit="dental_assistant_created"; break;
            default: throw new ApiException(400,"Operación no permitida.");
        }
        if (!hasRole) db.UserRoles.Add(new UserRole { UserId=userId, RoleId=role.Id, AssignedByUserId=support.Actor });
        support.Audit(audit,entity,profileId);
        if (!hasRole) support.Audit("user_role_added","User",userId, metadata:new { roleCode=code });
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return await Response(userId);
    }
}
