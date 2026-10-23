using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;

public sealed class PatientsTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private const string Path = "/api/admin/patients";
    private async Task<HttpClient> Admin()
    {
        var c = f.Client(); var login = await f.Login(c);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Body.AccessToken);
        return c;
    }
    private static Dictionary<string, object?> Body(string? documentNumber = null) => new()
    {
        ["firstName"] = "Paciente", ["lastName"] = "Prueba",
        ["documentType"]="CI", ["documentCountryCode"]="BO", ["documentNumber"] = documentNumber ?? "CI-" + Guid.NewGuid(),
        ["phone"] = "70000000", ["emergencyContactName"] = "Test contact", ["emergencyContactPhone"] = "71111111", ["bloodType"] = "UNKNOWN", ["notes"] = "Nota clínica sintética",
        ["birthDate"] = "1995-05-10", ["isActive"] = true
    };
    private static async Task<PatientResponse> Create(HttpClient c, Dictionary<string, object?>? body = null)
    {
        var response = await c.PostAsJsonAsync(Path, body ?? Body());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var patient = (await response.Content.ReadFromJsonAsync<PatientResponse>())!;
        Assert.EndsWith(patient.Id.ToString(), response.Headers.Location!.ToString());
        return patient;
    }
    private Dictionary<string, object?> WithAccount(string? email = null)
    {
        var body = Body(); body["account"] = new { email = email ?? Guid.NewGuid() + "@example.test", password = f.Password, confirmPassword = f.Password }; return body;
    }
    private async Task<HttpClient> WithPermissions(params string[] permissions)
    {
        var email = await f.CreateUser();
        using (var scope = f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var userId = await db.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
            var role = new Role { Code = "TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Name = Guid.NewGuid().ToString(), IsActive = true };
            db.Roles.Add(role);
            foreach (var id in await db.Permissions.Where(x => permissions.Contains(x.Code)).Select(x => x.Id).ToArrayAsync())
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = id });
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id }); await db.SaveChangesAsync();
        }
        var c = f.Client(); var login = await f.Login(c, email);
        c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken); return c;
    }

    [Fact] public async Task CrearUserSinAccesoYPerfil()
    {
        using var c = await Admin(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.Users.CountAsync(); var p = await Create(c);
        Assert.NotEqual(Guid.Empty, p.UserId); Assert.False(p.CanLogin); Assert.Null(p.Email); Assert.NotNull(p.DocumentNumber); Assert.True(p.IsActive);
        Assert.Equal(before + 1, await db.Users.CountAsync());
        var person = await db.Users.SingleAsync(x => x.Id == p.UserId); Assert.False(person.CanLogin); Assert.Null(person.PasswordHash); Assert.Null(person.Email);
        Assert.True(await db.UserRoles.AnyAsync(x => x.UserId == p.UserId && x.Role.Code == "PATIENT")); Assert.Equal(DateTimeKind.Utc, p.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, p.UpdatedAt.Kind);
    }
    [Fact] public async Task CrearConCuentaAsignaPatientYSeparaEmailClinico()
    {
        using var c = await Admin(); var email = Guid.NewGuid() + "@example.test";
        var p = await Create(c, WithAccount(email)); Assert.True(p.CanLogin); Assert.Equal(email, p.Email);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(email, (await db.Users.SingleAsync(x => x.Id == p.UserId)).Email);
        Assert.True(await db.UserRoles.AnyAsync(x => x.UserId == p.UserId && x.Role.Code == "PATIENT"));
        var update = Body();
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"{Path}/{p.Id}", update)).StatusCode);
        Assert.Equal(email, (await db.Users.AsNoTracking().SingleAsync(x => x.Id == p.UserId)).Email);
    }
    [Fact] public async Task NoAceptaUserIdManual()
    {
        using var c = await Admin(); var body = Body(); body["userId"] = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, body)).StatusCode);
        var p = await Create(c);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"{Path}/{p.Id}", body)).StatusCode);
    }
    [Fact] public async Task EmailDuplicadoReviertePerfilYCuenta()
    {
        using var c = await Admin(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Users.CountAsync(); var profiles = await db.PatientProfiles.CountAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Path, WithAccount(f.Email.ToUpperInvariant()))).StatusCode);
        Assert.Equal(users, await db.Users.CountAsync()); Assert.Equal(profiles, await db.PatientProfiles.CountAsync());
    }
    [Fact] public async Task CuentaPosteriorSoloUnaVezYLogin()
    {
        using var c = await Admin(); var p = await Create(c); var email = Guid.NewGuid() + "@example.test";
        var account = new { email, password = f.Password, confirmPassword = f.Password };
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var usersBefore = await db.Users.CountAsync();
        var response = await c.PostAsJsonAsync($"{Path}/{p.Id}/account", account); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<PatientResponse>())!; Assert.Equal(p.UserId, updated.UserId); Assert.True(updated.CanLogin);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Path}/{p.Id}/account", account)).StatusCode);
        using var patientClient = f.Client(); var login = await f.Login(patientClient, email); Assert.Contains("PATIENT", login.Body.User.Roles);
        Assert.Equal(usersBefore, await db.Users.CountAsync());
    }

    [Fact] public async Task SinAccesoNoAutenticaPeroApareceEnIdentidades()
    {
        using var c = await Admin(); var p = await Create(c);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == p.UserId);
        user.Email = Guid.NewGuid() + "@example.test"; await db.SaveChangesAsync();
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/auth/login", new { email = user.Email, password = f.Password })).StatusCode);
        var list = (await c.GetFromJsonAsync<ClinicaRamirez.Api.DTOs.Identity.UserListResponse>("/api/admin/users?pageSize=100"))!;
        Assert.Contains(list.Items, x => x.Id == p.UserId && !x.CanLogin);
        Assert.Equal(await db.Users.CountAsync(), list.Total);
    }

    [Fact] public async Task DeshabilitarCredencialesRechazaAccessYRefresh()
    {
        using var c = await Admin(); var email = Guid.NewGuid() + "@example.test"; var p = await Create(c, WithAccount(email));
        using var loginClient = f.Client(); var login = await f.Login(loginClient, email);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Id == p.UserId); user.CanLogin = false; user.PasswordHash = null; await db.SaveChangesAsync();
        loginClient.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await loginClient.GetAsync("/api/patient/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await IdentityFixture.Refresh(loginClient, login.Cookie)).StatusCode);
    }

    [Fact] public void PerfilNoDuplicaDatosPersonales()
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = db.Model.FindEntityType(typeof(ClinicaRamirez.Api.Domain.Patients.PatientProfile))!;
        foreach (var name in new[] { "FirstName", "LastName", "DocumentNumber", "BirthDate", "Gender", "Phone", "Email", "Address" })
            Assert.Null(profile.FindProperty(name));
        Assert.False(profile.FindProperty("UserId")!.IsNullable);
        Assert.Equal(DeleteBehavior.Restrict, profile.GetForeignKeys().Single().DeleteBehavior);
    }
    [Fact] public async Task CiDuplicadoNormalizado409InclusoInactivo()
    {
        using var c = await Admin(); var documentNumber= "CI-" + Guid.NewGuid(); var p = await Create(c, Body(documentNumber: documentNumber));
        await c.PatchAsJsonAsync($"{Path}/{p.Id}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Path, Body(documentNumber: " " + documentNumber.ToLowerInvariant() + " "))).StatusCode);
    }
    [Fact] public async Task CiVacioRechazado()
    { using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, Body(documentNumber: "  "))).StatusCode); }
    [Fact] public async Task ListaPaginada()
    {
        using var c = await Admin(); await Create(c);
        var list = (await c.GetFromJsonAsync<PatientListResponse>(Path + "?page=1&pageSize=1"))!;
        Assert.Single(list.Items); Assert.True(list.Total >= 1); Assert.Equal(1, list.PageSize);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync(Path + "?page=0")).StatusCode);
    }
    [Fact] public async Task ObtienePaciente()
    {
        using var c = await Admin(); var p = await Create(c);
        var detail = await c.GetFromJsonAsync<PatientResponse>($"{Path}/{p.Id}"); Assert.Equal(p, detail);
    }
    [Fact] public async Task EditaSinRecrearCuenta()
    {
        using var c = await Admin(); var p = await Create(c, WithAccount()); var body = Body(); body["firstName"] = "Actualizado";
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var count = await db.Users.CountAsync();
        var response = await c.PutAsJsonAsync($"{Path}/{p.Id}", body); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<PatientResponse>())!;
        Assert.Equal(p.UserId, updated.UserId); Assert.Equal("Actualizado", updated.FirstName); Assert.Equal(count, await db.Users.CountAsync());
    }
    [Fact] public async Task DesactivaSinBorrarNiDesactivarUsuario()
    {
        using var c = await Admin(); var p = await Create(c, WithAccount()); var userId = p.UserId;
        Assert.Equal(HttpStatusCode.NoContent, (await c.PatchAsJsonAsync($"{Path}/{p.Id}/status", new { isActive = false })).StatusCode);
        Assert.False((await c.GetFromJsonAsync<PatientResponse>($"{Path}/{p.Id}"))!.IsActive);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Users.AnyAsync(x => x.Id == userId && x.IsActive));
    }
    [Theory] [InlineData("GET")] [InlineData("POST")] [InlineData("PUT")] [InlineData("PATCH")]
    public async Task SinPermiso403(string method)
    {
        using var c = await WithPermissions();
        var path = method is "GET" or "POST" ? Path : $"{Path}/{Guid.NewGuid()}" + (method == "PATCH" ? "/status" : "");
        using var request = new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(Body()) };
        Assert.Equal(HttpStatusCode.Forbidden, (await c.SendAsync(request)).StatusCode);
    }
    [Fact] public async Task ConPermisosExplicitos()
    {
        using var c = await WithPermissions("patients.view", "patients.create", "patients.update", "patients.disable");
        var p = await Create(c); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"{Path}/{p.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"{Path}/{p.Id}", Body())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PatchAsJsonAsync($"{Path}/{p.Id}/status", new { isActive = false })).StatusCode);
    }
    [Fact] public async Task UpdateNoEvadeDisable()
    {
        using var admin = await Admin(); var p = await Create(admin); using var c = await WithPermissions("patients.update");
        var body = Body(); body["isActive"] = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"{Path}/{p.Id}", body)).StatusCode);
    }
    [Fact] public async Task AuditoriaSinDatosClinicos()
    {
        using var c = await Admin(); var p = await Create(c, WithAccount());
        await c.PutAsJsonAsync($"{Path}/{p.Id}", Body()); await c.PatchAsJsonAsync($"{Path}/{p.Id}/status", new { isActive = false });
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = await db.AuditLogs.Where(x => x.Entity == "PatientProfile" && x.EntityId == p.Id).ToArrayAsync();
        foreach (var action in new[] { "patient_created", "patient_updated", "patient_status_changed", "patient_account_enabled" })
            Assert.Contains(events, x => x.Action == action && x.UserId != null);
        Assert.All(events, x => { Assert.DoesNotContain("clínica", x.MetadataJson ?? ""); Assert.DoesNotContain("clinical@example", x.MetadataJson ?? ""); });
    }
    [Theory] [InlineData("firstName")] [InlineData("lastName")] [InlineData("documentNumber")] [InlineData("birthDate")] [InlineData("phone")] [InlineData("emergencyContactName")] [InlineData("emergencyContactPhone")] [InlineData("bloodType")]
    public async Task NombresObligatorios(string field)
    {
        using var c = await Admin(); var body = Body(); body[field] = " ";
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, body)).StatusCode);
    }
    [Fact] public async Task Inexistente404YSinToken401()
    {
        using var c = await Admin(); var path = $"{Path}/{Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync(path, Body())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PatchAsJsonAsync(path + "/status", new { isActive = false })).StatusCode);
        using var anonymous = f.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path)).StatusCode);
    }
    [Fact] public async Task NoBorradoFisico()
    {
        using var c = await Admin(); var p = await Create(c);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PatientProfiles.Remove(await db.PatientProfiles.SingleAsync(x => x.Id == p.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
    [Fact] public async Task RolPatientProtegidoMientrasVinculado()
    {
        using var c = await Admin(); var patient = await Create(c, WithAccount()); var id = patient.UserId;
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync($"/api/admin/users/{id}/roles", new { roleIds = Array.Empty<Guid>() })).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(x => x.Code == "PATIENT");
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/admin/roles/{role.Id}", new { name = role.Name, isActive = false })).StatusCode);
    }
    [Theory][InlineData(false)][InlineData(true)] public async Task FechaLocalEnAltaYEdicion(bool future) {
        using var c = await Admin(); var p = await Create(c); var body = Body();
        body["birthDate"] = ClinicaRamirez.Api.DTOs.LocalBirthDateAttribute.Today.AddDays(future ? 1 : 0).ToString("yyyy-MM-dd");
        Assert.Equal(future ? HttpStatusCode.BadRequest : HttpStatusCode.Created, (await c.PostAsJsonAsync(Path, body)).StatusCode);
        body["documentNumber"] = p.DocumentNumber;
        Assert.Equal(future ? HttpStatusCode.BadRequest : HttpStatusCode.OK, (await c.PutAsJsonAsync($"{Path}/{p.Id}", body)).StatusCode);
    }
    [Fact] public async Task NoAsignacionManualPatientYConservaExistentes() {
        using var c = await Admin(); var p = await Create(c, WithAccount());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(x => x.Code == "PATIENT");
        var email = await f.CreateUser(); var other = await db.Users.SingleAsync(x => x.Email == email);
        foreach (var id in new[] { other.Id, p.UserId })
            Assert.Equal(HttpStatusCode.BadRequest, (await c.PutAsJsonAsync($"/api/admin/users/{id}/roles", new { roleIds = new[] { role.Id } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync($"/api/admin/users/{p.UserId}/roles", new { roleIds = Array.Empty<Guid>() })).StatusCode);
        Assert.True(await db.UserRoles.AnyAsync(x => x.UserId == p.UserId && x.RoleId == role.Id));
        Assert.False(await db.UserRoles.AnyAsync(x => x.UserId == other.Id && x.RoleId == role.Id));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/admin/users", new { firstName = "Test", lastName = "Test", email = Guid.NewGuid() + "@example.test", password = f.Password, roleIds = new[] { role.Id } })).StatusCode);
    }
    [Fact] public async Task CuentaPosteriorRespetaAutorizacionYEmailGlobal() {
        using var c = await Admin(); var p = await Create(c);
        var account = new { email = f.Email, password = f.Password, confirmPassword = f.Password };
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync($"{Path}/{p.Id}/account", account)).StatusCode);
        using var denied = await WithPermissions("patients.view");
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.PostAsJsonAsync($"{Path}/{p.Id}/account", account)).StatusCode);
        Assert.False((await c.GetFromJsonAsync<PatientResponse>($"{Path}/{p.Id}"))!.CanLogin);
    }
    [Fact] public async Task MigracionYSeedsPreviosCompatibles()
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), x => x.EndsWith("_AddPatients"));
        await scope.ServiceProvider.GetRequiredService<IdentitySeed>().Run();
        await scope.ServiceProvider.GetRequiredService<ClinicSeed>().Run();
        Assert.Equal(IdentitySeed.PermissionCodes.Length, await db.Permissions.CountAsync()); Assert.Equal(1, await db.Clinics.CountAsync()); Assert.Equal(12, await db.Specialties.CountAsync());
    }
}
