using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.DTOs.Identity;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.Identity;

public sealed partial class AuthService
{
    public const string PatientOnlyMessage = "Google sign-in is available only to patients. Please sign in with your email and password.";
    public static bool Incomplete(User u, PatientProfile p) => string.IsNullOrWhiteSpace(u.FirstName)
        || string.IsNullOrWhiteSpace(u.LastName) || string.IsNullOrWhiteSpace(u.DocumentNumber)
        || u.BirthDate == null || string.IsNullOrWhiteSpace(u.Phone)
        || string.IsNullOrWhiteSpace(p.EmergencyContactPhone) || string.IsNullOrWhiteSpace(p.BloodType);

    public async Task EnsureGooglePatient(Guid id)
    {
        var roles = await db.UserRoles.Where(x => x.UserId == id).Select(x => new { x.Role.Code, x.Role.IsActive }).ToArrayAsync();
        if (roles.Length != 1 || roles[0].Code != "PATIENT" || !roles[0].IsActive)
            throw new ApiException(403, PatientOnlyMessage);
        if (!await db.PatientProfiles.AnyAsync(x => x.UserId == id && x.IsActive))
            throw new ApiException(403, "Patient account unavailable.");
    }

    public async Task<(LoginResponse Response, string Cookie, DateTime Expiry)> Google(GoogleIdentity identity)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        var email = identity.Email.Trim().ToLowerInvariant();
        var linked = await db.UserExternalLogins.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Provider == "GOOGLE" && x.ProviderSubject == identity.Subject);
        var byEmail = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (linked != null && byEmail != null && linked.UserId != byEmail.Id)
            throw new ApiException(409, "Google identity is already linked to another account.");
        var user = linked?.User ?? byEmail;
        if (user == null)
        {
            var role = await db.Roles.SingleAsync(x => x.Code == "PATIENT" && x.IsActive);
            user = new User { FirstName = identity.FirstName, LastName = identity.LastName, Email = email, EmailVerified = true };
            db.Users.Add(user);
            db.UserRoles.Add(new UserRole { User = user, RoleId = role.Id });
            db.PatientProfiles.Add(new PatientProfile { User = user });
        }
        else
        {
            await support.LockSessions(user.Id);
            if (!user.IsActive || !user.CanLogin) throw new ApiException(401, "Account unavailable.");
            await EnsureGooglePatient(user.Id);
            if (linked == null && await db.UserExternalLogins.AnyAsync(x => x.UserId == user.Id && x.Provider == "GOOGLE"))
                throw new ApiException(409, "A different Google identity is already linked.");
        }
        if (linked == null)
            db.UserExternalLogins.Add(new UserExternalLogin { User = user, UserId = user.Id, ProviderSubject = identity.Subject, ProviderEmail = email });
        user.LastLoginAt = DateTime.UtcNow;
        var session = NewToken(user, out var raw);
        session.PatientOnly = true;
        db.RefreshTokens.Add(session);
        support.Audit("google_patient_login", "User", user.Id, user.Id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (await Access(user, session), raw, session.ExpiresAt);
    }
}
