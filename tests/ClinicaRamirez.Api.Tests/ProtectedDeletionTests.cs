using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Application.Deletion;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ClinicaRamirez.Api.Tests;

public sealed class ProtectedDeletionTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private static string Path(string kind, Guid id) => $"/api/admin/deletion/{kind}/{id}";
    private async Task<HttpClient> Admin() {
        var c = f.Client(); var login = await f.Login(c); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken); return c;
    }
    private static Task<HttpResponseMessage> Delete(HttpClient c, string kind, Guid id, string password) =>
        c.SendAsync(new HttpRequestMessage(HttpMethod.Delete, Path(kind, id)) { Content = JsonContent.Create(new { password }) });
    private async Task<T> Save<T>(T entity) where T : class {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Add(entity); await db.SaveChangesAsync(); return entity;
    }
    private Task<Center> Center(bool active = false) => Save(new Center { ClinicId = Clinic.SingletonId, Name = "Delete test", Code = "TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Address = "Test", IsActive = active });
    private Task<User> User(bool active = false) => Save(new User { FirstName = "Test", LastName = "Delete", Email = Guid.NewGuid() + "@example.test", PasswordHash = "unused", IsActive = active });
    private async Task<DoctorProfile> Doctor() => await Save(new DoctorProfile { ProfessionalLicense = "TEST-" + Guid.NewGuid().ToString("N").ToUpperInvariant(), UserId = (await User()).Id, IsActive = false });
    private Task<Specialty> Specialty() => Save(new Specialty { Code = "TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Name = Guid.NewGuid().ToString(), IsActive = false });
    private Task<PatientProfile> Patient() => Save(new PatientProfile { User = new User { CanLogin = false, Ci = "TEST" + Guid.NewGuid().ToString("N").ToUpperInvariant(), BirthDate = new(1995, 5, 10), Phone = "70000000", FirstName = "Test", LastName = "Delete" }, EmergencyContactName = "Test contact", EmergencyContactPhone = "71111111", BloodType = "UNKNOWN", IsActive = false });
    private async Task<Appointment> Appointment(Guid? centerId = null, Guid? doctorId = null, Guid? patientId = null, Guid? specialtyId = null, Guid? creator = null) => await Save(new Appointment {
        CenterId = centerId ?? (await Center()).Id, DoctorId = doctorId ?? (await Doctor()).Id,
        PatientId = patientId ?? (await Patient()).Id, SpecialtyId = specialtyId ?? (await Specialty()).Id,
        CreatedByUserId = creator, AppointmentDate = new(2027, 1, 4), StartTime = new(9, 0), EndTime = new(10, 0), Status = "CANCELLED"
    });
    private async Task<DoctorSchedule> Schedule(Guid centerId, Guid doctorId) {
        await Save(new DoctorCenter { CenterId = centerId, DoctorId = doctorId, IsActive = false });
        return await Save(new DoctorSchedule { CenterId = centerId, DoctorId = doctorId, DayOfWeek = 1, StartTime = new(9, 0), EndTime = new(10, 0), IsActive = false });
    }
    private async Task AssertBlocked(HttpClient c, string kind, Guid id, string dependency) {
        var check = await c.GetAsync(Path(kind, id) + "/check"); Assert.Equal(HttpStatusCode.Conflict, check.StatusCode);
        var body = (await check.Content.ReadFromJsonAsync<DeletionCheck>())!; Assert.False(body.CanDelete);
        Assert.Contains(body.Dependencies, d => d.Type == dependency && d.Count > 0);
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(c, kind, id, f.Password)).StatusCode);
    }

    [Theory]
    [InlineData("centers")][InlineData("specialties")][InlineData("doctors")][InlineData("schedules")]
    [InlineData("patients")][InlineData("appointments")][InlineData("social-networks")][InlineData("users")][InlineData("roles")]
    public async Task FreeInactiveRecordDeletedAndAudited(string kind) {
        object entity = kind switch {
            "centers" => await Center(), "specialties" => await Specialty(), "doctors" => await Doctor(),
            "schedules" => await Schedule((await Center()).Id, (await Doctor()).Id),
            "patients" => await Patient(), "appointments" => await Appointment(), "users" => await User(),
            "roles" => await Save(new Role { Code = "TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Name = Guid.NewGuid().ToString(), IsActive = false }),
            _ => await Save(new ClinicSocialNetwork { ClinicId = Clinic.SingletonId, Name = "Test", Url = "https://example.test", IsActive = false })
        };
        var id = (Guid)entity.GetType().GetProperty("Id")!.GetValue(entity)!;
        using var c = await Admin();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(Path(kind, id) + "/check")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Delete(c, kind, id, f.Password)).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null(await db.FindAsync(entity.GetType(), id));
        var audit = await db.AuditLogs.SingleAsync(x => x.EntityId == id && x.Action.EndsWith("_deleted"));
        Assert.Equal(entity.GetType().Name, audit.Entity); Assert.NotNull(audit.UserId);
        using var json = System.Text.Json.JsonDocument.Parse(audit.MetadataJson!);
        Assert.Equal(3, json.RootElement.EnumerateObject().Count()); Assert.Equal(id, json.RootElement.GetProperty("entityId").GetGuid());
        Assert.DoesNotContain(f.Password, audit.MetadataJson!); Assert.DoesNotContain("password", audit.MetadataJson!, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task ActiveRequiresDeactivation() {
        using var c = await Admin(); var center = await Center(true);
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(c, "centers", center.Id, f.Password)).StatusCode);
    }
    [Theory][InlineData("SCHEDULED")][InlineData("CONFIRMED")][InlineData("IN_PROGRESS")][InlineData("COMPLETED")]
    public async Task NonCancelledAppointmentProtected(string status) {
        var appointment = await Appointment();
        using (var scope = f.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Appointments.Where(x => x.Id == appointment.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status)); }
        using var c = await Admin(); Assert.Equal(HttpStatusCode.Conflict, (await Delete(c, "appointments", appointment.Id, f.Password)).StatusCode);
    }
    [Fact] public async Task CenterWithDoctorCenter() {
        var center = await Center(); var doctor = await Doctor(); await Save(new DoctorCenter { CenterId = center.Id, DoctorId = doctor.Id, IsActive = false });
        using var c = await Admin(); await AssertBlocked(c, "centers", center.Id, "DoctorCenters");
    }
    [Fact] public async Task CenterWithScheduleNoCascade() {
        var center = await Center(); var schedule = await Schedule(center.Id, (await Doctor()).Id);
        using var c = await Admin(); await AssertBlocked(c, "centers", center.Id, "DoctorSchedules");
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Centers.AnyAsync(x => x.Id == center.Id)); Assert.True(await db.DoctorSchedules.AnyAsync(x => x.Id == schedule.Id));
        Assert.True(await db.DoctorCenters.AnyAsync(x => x.CenterId == center.Id));
    }
    [Theory][InlineData("centers")][InlineData("doctors")][InlineData("specialties")][InlineData("patients")][InlineData("users")]
    public async Task AppointmentReferencesBlockAllParents(string kind) {
        var user = await User(); var a = await Appointment(creator: user.Id);
        var id = kind switch { "centers" => a.CenterId, "doctors" => a.DoctorId, "specialties" => a.SpecialtyId, "patients" => a.PatientId, _ => user.Id };
        using var c = await Admin(); await AssertBlocked(c, kind, id, "Appointments");
    }
    [Theory][InlineData("doctors")][InlineData("specialties")]
    public async Task SpecialtyAssignmentsBlock(string kind) {
        var doctor = await Doctor(); var specialty = await Specialty(); await Save(new DoctorSpecialty { DoctorId = doctor.Id, SpecialtyId = specialty.Id });
        using var c = await Admin(); await AssertBlocked(c, kind, kind == "doctors" ? doctor.Id : specialty.Id, "DoctorSpecialties");
    }
    [Theory][InlineData("DoctorProfiles")][InlineData("PatientProfiles")][InlineData("AuditLogs")][InlineData("RefreshTokens")][InlineData("UserRoles")][InlineData("AssignedBy")]
    public async Task UserReferencesBlock(string dependency) {
        var user = await User(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        switch (dependency) {
            case "DoctorProfiles": await Save(new DoctorProfile { ProfessionalLicense = "TEST-" + Guid.NewGuid().ToString("N").ToUpperInvariant(), UserId = user.Id }); break;
            case "PatientProfiles": await Save(new PatientProfile { EmergencyContactName = "Test contact", EmergencyContactPhone = "71111111", BloodType = "UNKNOWN", UserId = user.Id }); break;
            case "AuditLogs": await Save(new AuditLog { UserId = user.Id, Action = "test", Entity = "User" }); break;
            case "RefreshTokens": await Save(new RefreshToken { UserId = user.Id, TokenHash = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), CreatedAt = DateTime.UtcNow.AddDays(-2), ExpiresAt = DateTime.UtcNow.AddDays(-1), RevokedAt = DateTime.UtcNow }); break;
            default: await Save(new UserRole { UserId = dependency == "AssignedBy" ? (await User()).Id : user.Id, RoleId = await db.Roles.Select(x => x.Id).FirstAsync(), AssignedByUserId = dependency == "AssignedBy" ? user.Id : null }); break;
        }
        using var c = await Admin(); await AssertBlocked(c, "users", user.Id, dependency == "AssignedBy" ? "UserRoles" : dependency);
    }
    [Theory][InlineData(true)][InlineData(false)] public async Task RoleReferencesBlock(bool permissions) {
        var role = await Save(new Role { Code = "TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Name = Guid.NewGuid().ToString(), IsActive = false });
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (permissions) await Save(new RolePermission { RoleId = role.Id, PermissionId = await db.Permissions.Select(x => x.Id).FirstAsync() });
        else await Save(new UserRole { RoleId = role.Id, UserId = (await User()).Id });
        using var c = await Admin(); await AssertBlocked(c, "roles", role.Id, permissions ? "RolePermissions" : "UserRoles");
    }
    [Fact] public async Task WrongPasswordDoesNotDeleteOrAudit() {
        var center = await Center(); using var c = await Admin();
        var response = await Delete(c, "centers", center.Id, "another-users-password"); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("incorrecta", await response.Content.ReadAsStringAsync());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Centers.AnyAsync(x => x.Id == center.Id)); Assert.False(await db.AuditLogs.AnyAsync(x => x.EntityId == center.Id));
    }
    [Fact] public async Task PasswordRequired() {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Delete(c, "centers", (await Center()).Id, "")).StatusCode);
    }
    [Fact] public async Task UnauthenticatedCannotDelete() {
        using var c = f.Client(); var id = (await Center()).Id;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Delete(c, "centers", id, f.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(Path("centers", id) + "/check")).StatusCode);
    }
    [Fact] public async Task NonAdministratorCannotCheckOrDeleteEvenWithPassword() {
        using var c = f.Client(); var session = await f.Login(c, await f.CreateUser()); c.DefaultRequestHeaders.Authorization = new("Bearer", session.Body.AccessToken);
        var id = (await Center()).Id; Assert.Equal(HttpStatusCode.Forbidden, (await Delete(c, "centers", id, f.Password)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(Path("centers", id) + "/check")).StatusCode);
    }
    [Theory][InlineData("clinics")][InlineData("audit-logs")][InlineData("permissions")][InlineData("refresh-tokens")]
    [InlineData("user-roles")][InlineData("role-permissions")][InlineData("doctor-centers")][InlineData("doctor-specialties")][InlineData("__EFMigrationsHistory")][InlineData("future-entity")]
    public async Task ProtectedCategory(string kind) {
        using var c = await Admin(); var id = Guid.NewGuid();
        var response = await c.GetAsync(Path(kind, id) + "/check"); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<DeletionCheck>())!.Protected);
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(c, kind, id, f.Password)).StatusCode);
    }
    [Fact] public async Task SystemRolesAndInitialSpecialtiesProtected() {
        using var c = await Admin(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.FirstAsync(x => x.IsSystem); var specialty = await db.Specialties.SingleAsync(x => x.Code == "GENERAL_DENTISTRY");
        foreach (var (kind, id) in new[] { ("roles", role.Id), ("specialties", specialty.Id) }) {
            var response = await c.GetAsync(Path(kind, id) + "/check"); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.True((await response.Content.ReadFromJsonAsync<DeletionCheck>())!.Protected);
        }
    }
    [Fact] public async Task SelfAndLastAdministratorRemain() {
        using var c = await Admin(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await db.Users.SingleAsync(x => x.Email == f.Email);
        Assert.Equal(1, await db.UserRoles.CountAsync(x => x.User.IsActive && x.Role.Code == "ADMINISTRATOR"));
        var response = await Delete(c, "users", admin.Id, f.Password); Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<DeletionCheck>())!.Protected);
        Assert.True(await db.Users.AnyAsync(x => x.Id == admin.Id && x.IsActive));
    }
    [Fact] public async Task RechecksRelationsAddedAfterCheck() {
        using var c = await Admin(); var center = await Center();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(Path("centers", center.Id) + "/check")).StatusCode);
        await Save(new DoctorCenter { CenterId = center.Id, DoctorId = (await Doctor()).Id });
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(c, "centers", center.Id, f.Password)).StatusCode);
    }
    [Fact] public async Task ActualForeignKeyViolationReturns409AndRollsBack() {
        var center = await Center(); var doctor = await Doctor();
        var race = new InsertDependencyBeforeDelete(center.Id, doctor.Id);
        using var factory = f.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddDbContext<AppDbContext>(o => o.AddInterceptors(race))));
        using var c = factory.CreateClient(); c.DefaultRequestHeaders.Add("X-Clinic-Request", "1");
        var login = await f.Login(c); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        var response = await Delete(c, "centers", center.Id, f.Password); Assert.True(response.StatusCode == HttpStatusCode.Conflict, race.Failure?.ToString() ?? await response.Content.ReadAsStringAsync()); Assert.True(race.Triggered);
        Assert.Contains(Assert.IsType<PostgresException>(race.Failure).SqlState, new[] { "23503", "23001" });
        Assert.False((await response.Content.ReadFromJsonAsync<DeletionCheck>())!.CanDelete);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.Centers.AnyAsync(x => x.Id == center.Id));
        Assert.False(await db.AuditLogs.AnyAsync(x => x.EntityId == center.Id));
        Assert.False(await db.DoctorCenters.AnyAsync(x => x.CenterId == center.Id)); // inserted in the same rolled-back transaction
    }
    [Theory][InlineData("CANCELLED")][InlineData("NO_SHOW")]
    public async Task EliminableAppointmentStillBlockedByFutureDependency(string status) {
        var free = await Appointment(); var linked = await Appointment();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Appointments.Where(x => x.Id == free.Id || x.Id == linked.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status));
        // Only in the fixture's isolated database: emulate a future incoming FK, without changing the application model.
        Assert.StartsWith("identity_test_", db.Database.GetDbConnection().Database);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "DeletionAppointmentDependencyTest" (
                "AppointmentId" uuid PRIMARY KEY REFERENCES "Appointments" ("Id") ON DELETE RESTRICT
            )
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"DeletionAppointmentDependencyTest\" (\"AppointmentId\") VALUES ({linked.Id})");
        using var c = await Admin();
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(Path("appointments", free.Id) + "/check")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Delete(c, "appointments", free.Id, f.Password)).StatusCode);
        Assert.False(await db.Appointments.AnyAsync(x => x.Id == free.Id));
        await AssertBlocked(c, "appointments", linked.Id, "DeletionAppointmentDependencyTest");
        Assert.True(await db.Appointments.AnyAsync(x => x.Id == linked.Id));
    }
    [Fact] public async Task RemovingAssignmentsPreservesAppointments() {
        var doctor = await Doctor(); var center = await Center(true); var specialty = await Specialty();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.DoctorProfiles.Where(x => x.Id == doctor.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, true));
        await Save(new DoctorCenter { DoctorId = doctor.Id, CenterId = center.Id });
        await Save(new DoctorSpecialty { DoctorId = doctor.Id, SpecialtyId = specialty.Id });
        var appointment = await Appointment(center.Id, doctor.Id, specialtyId: specialty.Id);
        using var c = await Admin();
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync($"/api/admin/doctors/{doctor.Id}/centers", new { centerIds = Array.Empty<Guid>() })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PutAsJsonAsync($"/api/admin/doctors/{doctor.Id}/specialties", new { specialtyIds = Array.Empty<Guid>() })).StatusCode);
        Assert.False(await db.DoctorCenters.AnyAsync(x => x.DoctorId == doctor.Id));
        Assert.False(await db.DoctorSpecialties.AnyAsync(x => x.DoctorId == doctor.Id));
        Assert.True(await db.Appointments.AnyAsync(x => x.Id == appointment.Id && x.CenterId == center.Id && x.SpecialtyId == specialty.Id));
    }
    [Fact] public async Task AllForeignKeysRemainRestrict() {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.All(db.Model.GetEntityTypes().SelectMany(t => t.GetForeignKeys()), fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
        await db.Database.OpenConnectionAsync(); using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM pg_constraint WHERE contype='f' AND connamespace='public'::regnamespace AND confdeltype <> 'r'";
        Assert.Equal(0L, Convert.ToInt64(await cmd.ExecuteScalarAsync()));
    }
    private sealed class InsertDependencyBeforeDelete(Guid center, Guid doctor) : DbCommandInterceptor {
        public bool Triggered { get; private set; }
        public Exception? Failure { get; private set; }
        public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData data, CancellationToken ct = default) { Failure = data.Exception; return Task.CompletedTask; }
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default) {
            if (!Triggered && command.CommandText.StartsWith("DELETE FROM \"public\".\"Centers\"")) {
                Triggered = true;
                using var insert = command.Connection!.CreateCommand(); insert.Transaction = command.Transaction;
                insert.CommandText = "INSERT INTO \"DoctorCenters\" (\"DoctorId\",\"CenterId\",\"AssignedAt\",\"IsActive\") VALUES (@doctor,@center,now(),false)";
                insert.Parameters.Add(new NpgsqlParameter("doctor", doctor)); insert.Parameters.Add(new NpgsqlParameter("center", center));
                await insert.ExecuteNonQueryAsync(ct);
            }
            return result;
        }
    }
}
