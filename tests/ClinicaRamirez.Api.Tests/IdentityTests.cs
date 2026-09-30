using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace ClinicaRamirez.Api.Tests;

public sealed class IdentityFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string Password { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public string Email { get; } = "admin@integration.example";
    private string connection = "";
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development")
        .UseSetting("ConnectionStrings:DefaultConnection", connection)
        .UseSetting("JWT_SECRET", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)))
        .UseSetting("BOOTSTRAP_ADMIN_EMAIL", Email).UseSetting("BOOTSTRAP_ADMIN_PASSWORD", Password)
        .UseSetting("LOGIN_RATE_LIMIT", "1000").UseSetting("Logging:LogLevel:Default", "Critical");
    public async Task InitializeAsync()
    {
        var supplied = Environment.GetEnvironmentVariable("IDENTITY_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Ejecute scripts/test-backend.ps1 para usar PostgreSQL de pruebas aislado.");
        var cs = new NpgsqlConnectionStringBuilder(supplied) { Database = "postgres" };
        await using var admin = new NpgsqlConnection(cs.ConnectionString); await admin.OpenAsync();
        var name = "identity_test_" + Guid.NewGuid().ToString("N");
        await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", admin); await command.ExecuteNonQueryAsync();
        cs.Database = name; connection = cs.ConnectionString;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IdentitySeed>().Run();
    }
    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask; // Conservar la base aislada para inspección; nunca borrar desarrollo.
    public HttpClient Client() { var client = CreateClient(new() { HandleCookies = false }); client.DefaultRequestHeaders.Add("X-Clinic-Request", "1"); return client; }
    public async Task<(LoginResponse Body, string Cookie)> Login(HttpClient client, string? email = null, string? password = null)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = email ?? Email, password = password ?? Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        return (body, response.Headers.GetValues("Set-Cookie").Single().Split(';')[0]);
    }
    public async Task<string> CreateUser(bool active = true)
    {
        using var scope = Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = new User { FirstName = "Prueba", LastName = "Integración", Email = Guid.NewGuid() + "@example.com", IsActive = active };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, Password);
        db.Users.Add(user); await db.SaveChangesAsync(); return user.Email;
    }
    public static async Task<HttpResponseMessage> Refresh(HttpClient client, string cookie) { using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh"); request.Headers.Add("Cookie", cookie); return await client.SendAsync(request); }
}

public sealed class IdentityTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private async Task<HttpClient> Admin() { var c = f.Client(); var login = await f.Login(c); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Body.AccessToken); return c; }
    [Fact] public async Task LoginCorrecto() { using var c = f.Client(); var (body, cookie) = await f.Login(c); Assert.Contains("ADMINISTRATOR", body.User.Roles); Assert.Contains("clinic_refresh=", cookie); Assert.Contains("users.view", body.User.Permissions); }
    [Fact] public async Task LoginIncorrecto() { using var c = f.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email = f.Email, password = "incorrecta" })).StatusCode); }
    [Fact] public async Task UsuarioInactivo() { var email = await f.CreateUser(false); using var c = f.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/auth/login", new { email, password = f.Password })).StatusCode); }
    [Fact] public async Task EmailDuplicadoNormalizado() { using var c = await Admin(); Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync("/api/admin/users", new { firstName = "Otro", lastName = "Usuario", email = f.Email.ToUpperInvariant(), password = f.Password })).StatusCode); }
    [Fact] public async Task AccesoSinToken() { using var c = f.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/admin/users")).StatusCode); }
    [Fact] public async Task AccesoSinPermiso() { using var c = f.Client(); var s = await f.Login(c, await f.CreateUser()); c.DefaultRequestHeaders.Authorization = new("Bearer", s.Body.AccessToken); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/users")).StatusCode); }
    [Fact] public async Task AccesoConPermiso() { using var c = await Admin(); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/admin/users")).StatusCode); Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/auth/me")).StatusCode); }
    [Fact] public async Task RefreshValido() { using var c = f.Client(); var s = await f.Login(c, await f.CreateUser()); var r = await IdentityFixture.Refresh(c, s.Cookie); Assert.Equal(HttpStatusCode.OK, r.StatusCode); var cookie = r.Headers.GetValues("Set-Cookie").Single(); Assert.Contains("httponly", cookie.ToLowerInvariant()); Assert.Contains("samesite=strict", cookie.ToLowerInvariant()); Assert.NotEqual(s.Cookie, cookie.Split(';')[0]); }
    [Fact] public async Task RefreshReutilizadoRevocaReemplazo() { using var c = f.Client(); var s = await f.Login(c, await f.CreateUser()); var r = await IdentityFixture.Refresh(c, s.Cookie); var next = r.Headers.GetValues("Set-Cookie").Single().Split(';')[0]; Assert.Equal(HttpStatusCode.Unauthorized, (await IdentityFixture.Refresh(c, s.Cookie)).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await IdentityFixture.Refresh(c, next)).StatusCode); }
    [Fact] public async Task LogoutRevocaRefreshYAccess() { using var c = f.Client(); var s = await f.Login(c, await f.CreateUser()); c.DefaultRequestHeaders.Add("Cookie", s.Cookie); Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsync("/api/auth/logout", null)).StatusCode); c.DefaultRequestHeaders.Remove("Cookie"); Assert.Equal(HttpStatusCode.Unauthorized, (await IdentityFixture.Refresh(c, s.Cookie)).StatusCode); c.DefaultRequestHeaders.Authorization = new("Bearer", s.Body.AccessToken); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode); }
    [Fact] public async Task BootstrapIdempotente() { using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var users = await db.Users.CountAsync(); var grants = await db.RolePermissions.CountAsync(); await scope.ServiceProvider.GetRequiredService<IdentitySeed>().Run(); await scope.ServiceProvider.GetRequiredService<IdentitySeed>().Run(); Assert.Equal(users, await db.Users.CountAsync()); Assert.Equal(6, await db.Roles.CountAsync(x => x.IsSystem)); Assert.Equal(56, await db.Permissions.CountAsync()); Assert.Equal(grants, await db.RolePermissions.CountAsync()); Assert.Equal(1, await db.Users.CountAsync(x => x.Email == f.Email)); }
    [Fact] public async Task AsignarRolesYPermisosTieneEfectoInmediato() {
        using var admin = await Admin(); var email = await f.CreateUser(); using var c = f.Client(); var s = await f.Login(c, email); c.DefaultRequestHeaders.Authorization = new("Bearer", s.Body.AccessToken);
        var created = await admin.PostAsJsonAsync("/api/admin/roles", new { code = "TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), name = Guid.NewGuid().ToString(), description = "Prueba" }); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var role = (await created.Content.ReadFromJsonAsync<RoleResponse>())!;
        var permissions = (await admin.GetFromJsonAsync<PermissionResponse[]>("/api/admin/permissions"))!;
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/roles/{role.Id}/permissions", new { permissionIds = new[] { permissions.Single(p => p.Code == "users.view").Id } })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{s.Body.User.Id}/roles", new { roleIds = new[] { role.Id } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/admin/users")).StatusCode);
        await admin.PutAsJsonAsync($"/api/admin/roles/{role.Id}/permissions", new { permissionIds = Array.Empty<Guid>() }); Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/users")).StatusCode);
    }
    [Fact] public async Task DesactivarRevocaSesion() { using var admin = await Admin(); using var c = f.Client(); var s = await f.Login(c, await f.CreateUser()); Assert.Equal(HttpStatusCode.NoContent, (await admin.PatchAsJsonAsync($"/api/admin/users/{s.Body.User.Id}/status", new { isActive = false })).StatusCode); c.DefaultRequestHeaders.Authorization = new("Bearer", s.Body.AccessToken); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/auth/me")).StatusCode); Assert.Equal(HttpStatusCode.Unauthorized, (await IdentityFixture.Refresh(c, s.Cookie)).StatusCode); }
    [Fact] public async Task ValidacionYNoEncontrado() { using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync("/api/admin/users?page=0")).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"/api/admin/users/{Guid.NewGuid()}")).StatusCode); }
    [Fact] public async Task ProtegeUltimoAdministrador() { using var c = await Admin(); var me = (await c.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me"))!; Assert.Equal(HttpStatusCode.Conflict, (await c.PatchAsJsonAsync($"/api/admin/users/{me.Id}/status", new { isActive = false })).StatusCode); }
    [Fact] public async Task RechazaOrigenYCabeceraInvalida() { using var c = f.Client(); c.DefaultRequestHeaders.Remove("X-Clinic-Request"); Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync("/api/auth/login", new { email = f.Email, password = f.Password })).StatusCode); }
    [Fact] public async Task RefreshConcurrenteConLogoutNoDejaSesionActiva()
    {
        using var c = f.Client(); var s = await f.Login(c, await f.CreateUser());
        using var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"); logout.Headers.Add("Cookie", s.Cookie);
        var responses = await Task.WhenAll(IdentityFixture.Refresh(c, s.Cookie), c.SendAsync(logout));
        Assert.Equal(HttpStatusCode.NoContent, responses[1].StatusCode);
        Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        if (responses[0].StatusCode == HttpStatusCode.OK)
        {
            var next = responses[0].Headers.GetValues("Set-Cookie").Single().Split(';')[0];
            Assert.Equal(HttpStatusCode.Unauthorized, (await IdentityFixture.Refresh(c, next)).StatusCode);
        }
    }
    [Fact] public async Task EditarRolYConsultarDetalle()
    {
        using var c = await Admin();
        var response = await c.PostAsJsonAsync("/api/admin/roles", new { code = "EDIT_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), name = Guid.NewGuid().ToString(), description = "Inicial" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var role = (await response.Content.ReadFromJsonAsync<RoleResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"/api/admin/roles/{role.Id}", new { name = role.Name, description = "Actualizado", isActive = false })).StatusCode);
        var updated = (await c.GetFromJsonAsync<RoleResponse>($"/api/admin/roles/{role.Id}"))!;
        Assert.False(updated.IsActive); Assert.Equal("Actualizado", updated.Description);
    }
    [Fact] public async Task AuditoriaNoContieneCredenciales() { using var c = f.Client(); await f.Login(c); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var audit = await db.AuditLogs.ToArrayAsync(); Assert.Contains(audit, a => a.Action == "login_success"); Assert.DoesNotContain(audit, a => (a.MetadataJson ?? "").Contains(f.Password)); var entry = audit.First(); entry.Action = "tampered"; await Assert.ThrowsAsync<InvalidOperationException>(() => { db.Update(entry); return db.SaveChangesAsync(); }); }
}
