using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Application.Patients;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Controllers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record GoogleCredentialRequest([Required, MaxLength(16384)] string Credential);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record CompletePatientProfileRequest : PatientClinicalRequest;

[ApiController, Route("api/auth"), EnableRateLimiting("login")]
public sealed class GooglePatientController(AuthService auth, IGoogleIdentityVerifier verifier,
    IDataProtectionProvider protection, IWebHostEnvironment environment, AppDbContext db, IdentitySupport support) : ControllerBase
{
    const string NonceCookie = "clinic_google_nonce";
    CookieOptions Options(DateTime expiry) => new() { HttpOnly = true, Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Strict, Path = "/api/auth", Expires = expiry, IsEssential = true };
    [HttpPost("google/challenge"), AllowAnonymous]
    public IActionResult ChallengeGoogle()
    {
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiry = DateTime.UtcNow.AddMinutes(10);
        var value = protection.CreateProtector("GooglePatientNonce.v1").ToTimeLimitedDataProtector().Protect(nonce, TimeSpan.FromMinutes(10));
        Response.Cookies.Append(NonceCookie, value, Options(expiry));
        return Ok(new { nonce });
    }
    [HttpPost("google"), AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Google(GoogleCredentialRequest request, CancellationToken ct)
    {
        string nonce;
        try { nonce = protection.CreateProtector("GooglePatientNonce.v1").ToTimeLimitedDataProtector().Unprotect(Request.Cookies[NonceCookie] ?? ""); }
        catch (CryptographicException) { throw new ApiException(401, "Your Google account could not be verified."); }
        Response.Cookies.Delete(NonceCookie, Options(DateTime.UtcNow));
        var identity = await verifier.Verify(request.Credential, nonce, ct);
        var result = await auth.Google(identity);
        Response.Cookies.Append("clinic_refresh", result.Cookie, Options(result.Expiry));
        return Ok(result.Response);
    }
    async Task<ClinicaRamirez.Api.Domain.Patients.PatientProfile> Profile()
    {
        var id = support.Actor ?? throw new ApiException(401, "Sesión inválida.");
        await auth.EnsureGooglePatient(id);
        if (!await db.UserExternalLogins.AnyAsync(x => x.UserId == id)) throw new ApiException(403, "External patient account required.");
        return await db.PatientProfiles.Include(x => x.User).SingleAsync(x => x.UserId == id);
    }
    [HttpGet("patient-profile"), Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var p = await Profile(); var u = p.User;
        return Ok(new { u.FirstName, u.LastName, u.DocumentType, u.DocumentNumber, u.DocumentCountryCode,
            u.BirthDate, u.Gender, u.Phone, u.Address, p.EmergencyContactPhone, p.BloodType });
    }
    [HttpPut("patient-profile"), Authorize]
    public async Task<ActionResult<CurrentUserResponse>> Complete(CompletePatientProfileRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        var p = await Profile(); var u = p.User;
        // Only fill missing data; this endpoint cannot overwrite established clinical information.
        if (string.IsNullOrWhiteSpace(u.FirstName)) u.FirstName = request.FirstName.Trim();
        if (string.IsNullOrWhiteSpace(u.LastName)) u.LastName = request.LastName.Trim();
        if (string.IsNullOrWhiteSpace(u.DocumentNumber)) await IdentityDocument.Apply(db, u, request.DocumentType, request.DocumentNumber, request.DocumentCountryCode);
        u.BirthDate ??= request.BirthDate;
        if (string.IsNullOrWhiteSpace(u.Phone)) { RegistrationPhone.Validate(request.Phone); u.Phone = request.Phone!.Trim(); }
        if (string.IsNullOrWhiteSpace(p.EmergencyContactPhone)) { RegistrationPhone.Validate(request.EmergencyContactPhone); p.EmergencyContactPhone = request.EmergencyContactPhone!.Trim(); }
        if (string.IsNullOrWhiteSpace(p.BloodType)) p.BloodType = request.BloodType!;
        if (string.IsNullOrWhiteSpace(u.Gender)) u.Gender = request.Gender?.Trim();
        if (string.IsNullOrWhiteSpace(u.Address)) u.Address = request.Address?.Trim();
        if (AuthService.Incomplete(u, p)) throw new ApiException(400, "Completa los datos obligatorios del paciente.");
        support.Audit("patient_profile_completed", "PatientProfile", p.Id);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok((await support.Current(u.Id)) with { Permissions = [] });
    }
}
