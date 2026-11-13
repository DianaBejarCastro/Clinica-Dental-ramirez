using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace ClinicaRamirez.Api.Tests;

public sealed class GooglePatientAuthTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    sealed class Verifier(GoogleIdentity identity) : IGoogleIdentityVerifier
    {
        public Task<GoogleIdentity> Verify(string credential, string nonce, CancellationToken ct)
        {
            Assert.False(string.IsNullOrWhiteSpace(nonce));
            if (credential != "valid") throw new ApiException(401, "Your Google account could not be verified.");
            return Task.FromResult(identity);
        }
    }
    static GoogleIdentity Identity(string? email = null) => new(Guid.NewGuid().ToString("N"), email ?? $"google-{Guid.NewGuid():N}@example.test", "Ana", "Pérez");
    WebApplicationFactory<Program> Factory(GoogleIdentity identity) => f.WithWebHostBuilder(b => b.ConfigureServices(s => {
        s.RemoveAll<IGoogleIdentityVerifier>(); s.AddSingleton<IGoogleIdentityVerifier>(new Verifier(identity));
    }));
    static HttpClient Client(WebApplicationFactory<Program> app) { var c = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false }); c.DefaultRequestHeaders.Add("X-Clinic-Request", "1"); return c; }
    static async Task<HttpResponseMessage> SignIn(HttpClient c, string credential = "valid")
    {
        var challenge = await c.PostAsync("/api/auth/google/challenge", null); challenge.EnsureSuccessStatusCode();
        c.DefaultRequestHeaders.Remove("Cookie"); c.DefaultRequestHeaders.Add("Cookie", challenge.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
        return await c.PostAsJsonAsync("/api/auth/google", new { credential });
    }
    [Fact] public async Task CreatesOnceLinksSubjectIssuesLocalSessionAndRefreshesWithoutPassword()
    {
        var id = Identity(); using var app = Factory(id); using var c = Client(app);
        var r = await SignIn(c); r.EnsureSuccessStatusCode(); var login = (await r.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(new[] { "PATIENT" }, login.User.Roles); Assert.Empty(login.User.Permissions); Assert.True(login.User.RequiresProfileCompletion);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken); Assert.Equal("google_patient", jwt.Claims.Single(x => x.Type == "auth_context").Value);
        Assert.NotEqual("accounts.google.com", jwt.Issuer);
        var cookie = r.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("clinic_refresh=")).Split(';')[0];
        c.DefaultRequestHeaders.Remove("Cookie"); c.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsync("/api/auth/refresh", null)).StatusCode);
        var second = (await (await SignIn(c)).Content.ReadFromJsonAsync<LoginResponse>())!; Assert.Equal(login.User.Id, second.User.Id);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.Email == id.Email)); Assert.Equal(1, await db.PatientProfiles.CountAsync(x => x.UserId == login.User.Id));
        var user = await db.Users.SingleAsync(x => x.Id == login.User.Id); Assert.Null(user.PasswordHash); Assert.Null(user.Phone); Assert.Null(user.BirthDate);
        Assert.Equal(id.Subject, (await db.UserExternalLogins.SingleAsync(x => x.UserId == user.Id)).ProviderSubject);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email = id.Email, password = "invented-password" })).StatusCode);
    }
    [Theory][InlineData("DOCTOR")][InlineData("ADMINISTRATOR")][InlineData("SECRETARY")][InlineData("CASHIER")][InlineData("DENTAL_ASSISTANT")]
    public async Task InternalRolesRejectedWithoutLinkOrPatient(string role)
    {
        var email = await f.CreateUser(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email); db.UserRoles.Add(new() { UserId = user.Id, RoleId = await db.Roles.Where(x => x.Code == role).Select(x => x.Id).SingleAsync() }); await db.SaveChangesAsync();
        using var app = Factory(Identity(email.ToUpperInvariant())); using var c = Client(app);
        var r = await SignIn(c); Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode); Assert.Contains(AuthService.PatientOnlyMessage, await r.Content.ReadAsStringAsync());
        Assert.False(await db.PatientProfiles.AnyAsync(x => x.UserId == user.Id)); Assert.False(await db.UserExternalLogins.AnyAsync(x => x.UserId == user.Id));
        Assert.False(await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.Role.Code == "PATIENT"));
    }
    async Task<Guid> LocalPatient(string email)
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var u = await db.Users.SingleAsync(x => x.Email == email);
        u.DocumentType = "CI"; u.DocumentCountryCode = "BO"; u.DocumentNumber = Guid.NewGuid().ToString("N").ToUpperInvariant(); u.BirthDate = new(1990, 1, 1); u.Phone = "70000000";
        db.PatientProfiles.Add(new() { User = u, EmergencyContactPhone = "70000000", BloodType = "UNKNOWN" });
        db.UserRoles.Add(new() { UserId = u.Id, RoleId = await db.Roles.Where(x => x.Code == "PATIENT").Select(x => x.Id).SingleAsync() }); await db.SaveChangesAsync(); return u.Id;
    }
    [Fact] public async Task ExistingNormalizedPatientReusedAndLocalPasswordPreserved()
    {
        var email = await f.CreateUser(); var uid = await LocalPatient(email); using var app = Factory(Identity(email.ToUpperInvariant())); using var c = Client(app);
        var r = await SignIn(c); r.EnsureSuccessStatusCode(); var login = (await r.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(uid, login.User.Id); Assert.False(login.User.RequiresProfileCompletion);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/auth/login", new { email, password = f.Password })).StatusCode);
    }
    [Fact] public async Task MixedRolesBlockedAndLaterRoleAdditionInvalidatesGoogleSession()
    {
        var email = await f.CreateUser(); var uid = await LocalPatient(email); using var app = Factory(Identity(email)); using var c = Client(app);
        var r = await SignIn(c); r.EnsureSuccessStatusCode(); var login = (await r.Content.ReadFromJsonAsync<LoginResponse>())!;
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserRoles.Add(new() { UserId = uid, RoleId = await db.Roles.Where(x => x.Code == "DOCTOR").Select(x => x.Id).SingleAsync() }); await db.SaveChangesAsync();
        c.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SignIn(c)).StatusCode);
    }
    [Fact] public async Task MissingNonceAndInvalidCredentialRejected()
    {
        using var app = Factory(Identity()); using var c = Client(app);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/google", new { credential = "valid" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SignIn(c, "forged")).StatusCode);
    }
    [Fact] public async Task CompletionFillsMissingDataAndUnlocksPatientPortal()
    {
        using var app = Factory(Identity()); using var c = Client(app); var login = (await (await SignIn(c)).Content.ReadFromJsonAsync<LoginResponse>())!;
        c.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/patient/dashboard")).StatusCode);
        var body = new { firstName = "Cannot replace", lastName = "Cannot replace", documentType = "CI", documentNumber = Guid.NewGuid().ToString("N"), documentCountryCode = "BO", birthDate = "1990-01-01", phone = "+59165700344", emergencyContactPhone = "+59165700345", bloodType = "UNKNOWN" };
        var complete = await c.PutAsJsonAsync("/api/auth/patient-profile", body); complete.EnsureSuccessStatusCode();
        var user = (await complete.Content.ReadFromJsonAsync<CurrentUserResponse>())!; Assert.False(user.RequiresProfileCompletion); Assert.Equal("Ana", user.FirstName);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/patient/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/users")).StatusCode);
    }
    [Fact] public async Task DuplicateSubjectDatabaseConstraintAndRestrictForeignKey()
    {
        var identity = Identity(); using var app = Factory(identity); using var c = Client(app); var login = (await (await SignIn(c)).Content.ReadFromJsonAsync<LoginResponse>())!;
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.UserExternalLogins.Add(new() { UserId = login.User.Id, ProviderSubject = identity.Subject, ProviderEmail = identity.Email });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var entity = db.Model.FindEntityType(typeof(UserExternalLogin))!; Assert.All(entity.GetForeignKeys(), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
    [Fact] public async Task ConcurrentGoogleLoginsCreateOnlyOneIdentity()
    {
        var id = Identity(); using var app = Factory(id); using var a = Client(app); using var b = Client(app);
        var results = await Task.WhenAll(SignIn(a), SignIn(b)); Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal(1, await db.Users.CountAsync(x => x.Email == id.Email));
    }
}

public sealed class GoogleTokenValidationTests
{
    [Theory][InlineData("valid")][InlineData("expired")][InlineData("audience")][InlineData("issuer")][InlineData("unverified")][InlineData("subject")][InlineData("nonce")][InlineData("signature")]
    public void ValidatesSignedGoogleClaims(string scenario)
    {
        using var rsa = RSA.Create(2048); using var wrong = RSA.Create(2048); var key = new RsaSecurityKey(rsa) { KeyId = "test" };
        var claims = new List<Claim> { new("sub", scenario == "subject" ? "" : "subject"), new("email", "ana@example.test"), new("email_verified", scenario == "unverified" ? "false" : "true"), new("nonce", scenario == "nonce" ? "wrong" : "nonce"), new("given_name", "Ana"), new("family_name", "Pérez") };
        var token = new JwtSecurityToken(scenario == "issuer" ? "https://attacker.test" : "https://accounts.google.com", scenario == "audience" ? "wrong" : "client", claims,
            DateTime.UtcNow.AddHours(-1), scenario == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5), new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        var raw = new JwtSecurityTokenHandler().WriteToken(token); SecurityKey validationKey = scenario == "signature" ? new RsaSecurityKey(wrong) { KeyId = "test" } : key;
        if (scenario == "valid") Assert.Equal("subject", GoogleIdentityVerifier.Validate(raw, "nonce", "client", [validationKey]).Subject);
        else Assert.ThrowsAny<SecurityTokenException>(() => GoogleIdentityVerifier.Validate(raw, "nonce", "client", [validationKey]));
    }
}
