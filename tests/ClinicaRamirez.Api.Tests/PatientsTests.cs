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
    private static Dictionary<string, object?> Body(Guid? userId = null, string? ci = null) => new()
    {
        ["firstName"] = "Paciente", ["lastName"] = "Prueba", ["userId"] = userId,
        ["ci"] = ci, ["email"] = "clinical@example.test", ["notes"] = "Nota clínica sintética",
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
    private async Task<Guid> User(bool patient = true)
    {
        var email = await f.CreateUser();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = await db.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        if (patient)
        {
            var role = await db.Roles.SingleAsync(x => x.Code == "PATIENT");
            db.UserRoles.Add(new UserRole { UserId = id, RoleId = role.Id }); await db.SaveChangesAsync();
        }
        return id;
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

    [Fact] public async Task CrearSinUserNoCreaCuenta()
    {
        using var c = await Admin(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.Users.CountAsync(); var p = await Create(c);
        Assert.Null(p.UserId); Assert.Null(p.Ci); Assert.True(p.IsActive);
        Assert.Equal(before, await db.Users.CountAsync()); Assert.Equal(DateTimeKind.Utc, p.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, p.UpdatedAt.Kind);
    }
    [Fact] public async Task VincularPatientMantieneEmailYPasswordDelUsuario()
    {
        using var c = await Admin(); var id = await User();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        var p = await Create(c, Body(id)); Assert.Equal(id, p.UserId); Assert.Equal("clinical@example.test", p.Email);
        var after = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(user.Email, after.Email); Assert.Equal(user.PasswordHash, after.PasswordHash);
    }
    [Fact] public async Task RechazaUsuarioSinPatient()
    {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, Body(await User(false)))).StatusCode);
    }
    [Fact] public async Task UsuarioInexistente404()
    {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync(Path, Body(Guid.NewGuid()))).StatusCode);
    }
    [Fact] public async Task UsuarioVinculado409()
    {
        using var c = await Admin(); var id = await User(); await Create(c, Body(id));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Path, Body(id))).StatusCode);
    }
    [Fact] public async Task CiDuplicadoNormalizado409InclusoInactivo()
    {
        using var c = await Admin(); var ci = "CI-" + Guid.NewGuid(); var p = await Create(c, Body(ci: ci));
        await c.PatchAsJsonAsync($"{Path}/{p.Id}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Path, Body(ci: " " + ci.ToLowerInvariant() + " "))).StatusCode);
    }
    [Fact] public async Task PermiteMultiplesCiYUserNulos()
    {
        using var c = await Admin(); var a = await Create(c, Body(ci: "  ")); var b = await Create(c);
        Assert.Null(a.Ci); Assert.Null(b.Ci); Assert.NotEqual(a.Id, b.Id);
    }
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
    [Fact] public async Task EditaYVinculaPacienteExistente()
    {
        using var c = await Admin(); var p = await Create(c); var id = await User();
        var body = Body(id); body["firstName"] = "Actualizado";
        var response = await c.PutAsJsonAsync($"{Path}/{p.Id}", body); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<PatientResponse>())!;
        Assert.Equal(id, updated.UserId); Assert.Equal("Actualizado", updated.FirstName);
        Assert.Equal(p.CreatedAt, updated.CreatedAt); Assert.True(updated.UpdatedAt >= p.UpdatedAt);
    }
    [Fact] public async Task DesactivaSinBorrarNiDesactivarUsuario()
    {
        using var c = await Admin(); var userId = await User(); var p = await Create(c, Body(userId));
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
        using var c = await Admin(); var p = await Create(c, Body(await User()));
        await c.PutAsJsonAsync($"{Path}/{p.Id}", Body(p.UserId)); await c.PatchAsJsonAsync($"{Path}/{p.Id}/status", new { isActive = false });
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var events = await db.AuditLogs.Where(x => x.Entity == "PatientProfile" && x.EntityId == p.Id).ToArrayAsync();
        foreach (var action in new[] { "patient_created", "patient_updated", "patient_status_changed", "patient_user_linked" })
            Assert.Contains(events, x => x.Action == action && x.UserId != null);
        Assert.All(events, x => { Assert.DoesNotContain("clínica", x.MetadataJson ?? ""); Assert.DoesNotContain("clinical@example", x.MetadataJson ?? ""); });
    }
    [Theory] [InlineData("firstName")] [InlineData("lastName")]
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
        using var c = await Admin(); var id = await User(); await Create(c, Body(id));
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/admin/users/{id}/roles", new { roleIds = Array.Empty<Guid>() })).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(x => x.Code == "PATIENT");
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"/api/admin/roles/{role.Id}", new { name = role.Name, isActive = false })).StatusCode);
    }
    [Fact] public async Task MigracionYSeedsPreviosCompatibles()
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), x => x.EndsWith("_AddPatients"));
        await scope.ServiceProvider.GetRequiredService<IdentitySeed>().Run();
        await scope.ServiceProvider.GetRequiredService<ClinicSeed>().Run();
        Assert.Equal(56, await db.Permissions.CountAsync()); Assert.Equal(1, await db.Clinics.CountAsync()); Assert.Equal(12, await db.Specialties.CountAsync());
    }
}
