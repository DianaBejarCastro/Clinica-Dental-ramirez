using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Application.Patients;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;
public sealed class PatientRegistrationTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private const string Path = "/api/auth/register/patient";
    private PatientRegistrationRequest Data() => new(" Ana ", " Paciente ", Guid.NewGuid() + "@EXAMPLE.COM", f.Password, f.Password, "CI" + Guid.NewGuid().ToString("N").ToUpperInvariant());
    private HttpClient Client()
    {
        var client = f.WithWebHostBuilder(b => b.UseSetting("REGISTRATION_RATE_LIMIT", "1000")).CreateClient(new() { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Clinic-Request", "1"); return client;
    }
    private async Task Register(HttpClient c, PatientRegistrationRequest data) => Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync(Path, data)).StatusCode);
    private async Task Login(HttpClient c, string email) { var login = await f.Login(c, email); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken); }
    [Fact] public async Task RegistroTransaccionalHashRolAuditoriaYLogin()
    {
        using var c = Client(); var data = Data(); await Register(c, data);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == data.Email.ToLowerInvariant()); var patient = await db.PatientProfiles.SingleAsync(x => x.UserId == user.Id);
        Assert.Equal("Ana", user.FirstName); Assert.True(user.IsActive); Assert.True(patient.IsActive); Assert.Equal(data.Ci, patient.Ci);
        Assert.NotEqual(data.Password, user.PasswordHash); Assert.NotEqual(PasswordVerificationResult.Failed, scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().VerifyHashedPassword(user, user.PasswordHash, data.Password));
        Assert.Equal(new[] { "PATIENT" }, await db.UserRoles.Where(x => x.UserId == user.Id).Select(x => x.Role.Code).ToArrayAsync());
        var audit = await db.AuditLogs.SingleAsync(x => x.EntityId == patient.Id && x.Action == "patient_self_registered"); Assert.Null(audit.MetadataJson); Assert.Null(audit.UserId);
        await Login(c, data.Email); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/patient/me")).StatusCode);
        foreach (var path in new[] { "/api/admin/users", "/api/admin/patients", "/api/admin/appointments" }) Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(path)).StatusCode);
    }
    [Theory][InlineData("PATIENT", true)][InlineData("DOCTOR", true)][InlineData("ADMINISTRATOR", true)][InlineData("PATIENT", false)]
    public async Task EmailGlobalIncluyeOtrosRolesEInactivos(string role, bool active)
    {
        var data = Data(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { FirstName = "Existente", LastName = "Prueba", Email = data.Email.ToLowerInvariant(), PasswordHash = "no-login", IsActive = active };
        db.Users.Add(user); db.UserRoles.Add(new UserRole { User = user, RoleId = await db.Roles.Where(x => x.Code == role).Select(x => x.Id).SingleAsync() }); await db.SaveChangesAsync();
        using var c = Client(); var response = await c.PostAsJsonAsync(Path, data); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Ya existe una cuenta", await response.Content.ReadAsStringAsync());
    }
    [Fact] public async Task CiDuplicadoNoCreaUsuario()
    {
        using var c = Client(); var first = Data(); await Register(c, first); var second = Data() with { Ci = first.Ci!.ToLowerInvariant() };
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Path, second)).StatusCode);
        using var scope = f.Services.CreateScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(x => x.Email == second.Email.ToLowerInvariant()));
    }
    [Theory][InlineData("short", "short")][InlineData("LongPassword123", "DifferentPassword123")]
    public async Task ValidaPasswordYConfirmacion(string password, string confirmation)
    { using var c = Client(); Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, Data() with { Password = password, ConfirmPassword = confirmation })).StatusCode); }
    [Theory][InlineData("roleId")][InlineData("roles")][InlineData("role")][InlineData("userId")][InlineData("isActive")]
    public async Task RechazaCamposAdministrativos(string key)
    {
        using var c = Client(); var json = JsonSerializer.SerializeToNode(Data(), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        json[key] = "ADMINISTRATOR"; Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, json)).StatusCode);
    }
    [Fact] public async Task RolInactivoFallaSeguro()
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var role = await db.Roles.SingleAsync(x => x.Code == "PATIENT");
        role.IsActive = false; await db.SaveChangesAsync();
        try { using var c = Client(); Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.PostAsJsonAsync(Path, Data())).StatusCode); }
        finally { role.IsActive = true; await db.SaveChangesAsync(); }
    }
    private sealed class FailPatientSave : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        { if (eventData.Context!.ChangeTracker.Entries<PatientProfile>().Any(x => x.State == EntityState.Added)) throw new InvalidOperationException("Simulated profile failure"); return ValueTask.FromResult(result); }
    }
    [Fact] public async Task FalloDelPerfilRevierteUsuarioYRol()
    {
        var data = Data(); using var scope = f.Services.CreateScope(); var original = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(new FailPatientSave()).Options))
        {
            var service = new PatientRegistrationService(db, new IdentitySupport(db, new HttpContextAccessor()), new PasswordHasher<User>());
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.Register(data, default));
        }
        Assert.False(await original.Users.AnyAsync(x => x.Email == data.Email.ToLowerInvariant()));
        Assert.False(await original.PatientProfiles.AnyAsync(x => x.Email == data.Email.ToLowerInvariant()));
    }
    [Fact] public async Task PortalAisladoIgnoraPatientIdArbitrarioYRechazaNoPatient()
    {
        using var c = Client(); var first = Data(); var second = Data() with { FirstName = "Otro" }; await Register(c, first); await Register(c, second); await Login(c, first.Email);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mine = await db.PatientProfiles.SingleAsync(x => x.Email == first.Email.ToLowerInvariant()); var other = await db.PatientProfiles.SingleAsync(x => x.Email == second.Email.ToLowerInvariant());
        var doctorUser = new User { FirstName = "Doctor", LastName = "Prueba", Email = Guid.NewGuid() + "@example.com", PasswordHash = "test" };
        var doctor = new DoctorProfile { User = doctorUser }; var center = new Center { ClinicId = Clinic.SingletonId, Name = "Centro", Code = "T" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Address = "Dirección", Country = "Bolivia" };
        db.DoctorProfiles.Add(doctor); db.Centers.Add(center); await db.SaveChangesAsync();
        var specialty = await db.Specialties.Select(x => x.Id).FirstAsync();
        foreach (var id in new[] { mine.Id, other.Id }) db.Appointments.Add(new Appointment { PatientId = id, DoctorId = doctor.Id, CenterId = center.Id, SpecialtyId = specialty, AppointmentDate = new DateOnly(2027, 1, 1), StartTime = new TimeOnly(id == mine.Id ? 9 : 10, 0), EndTime = new TimeOnly(id == mine.Id ? 10 : 11, 0) });
        await db.SaveChangesAsync();
        var me = await c.GetFromJsonAsync<PatientAccountResponse>($"/api/patient/me?patientId={other.Id}"); Assert.Equal("Ana", me!.FirstName);
        var appointments = await c.GetFromJsonAsync<PatientAppointmentResponse[]>($"/api/patient/appointments?patientId={other.Id}"); Assert.Single(appointments!); Assert.Equal(new TimeOnly(9, 0), appointments![0].StartTime);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/patient/me/{other.Id}")).StatusCode);
        mine.IsActive = false; await db.SaveChangesAsync(); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/patient/me")).StatusCode);
        await Login(c, f.Email); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/patient/me")).StatusCode);
        c.DefaultRequestHeaders.Authorization = null; Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/patient/me")).StatusCode);
    }
}
