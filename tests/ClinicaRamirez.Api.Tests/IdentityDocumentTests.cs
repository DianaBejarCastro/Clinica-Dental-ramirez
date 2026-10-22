using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace ClinicaRamirez.Api.Tests;

public sealed class IdentityDocumentTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private async Task<HttpClient> Admin()
    {
        var c = f.Client();
        var login = await f.Login(c);
        c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        return c;
    }
    private static PatientCreateRequest Request(string type = "CI", string? country = "BO", string? number = null) => new()
    {
        FirstName = "Document", LastName = "Test", DocumentType = type,
        DocumentNumber = number ?? Guid.NewGuid().ToString("N"), DocumentCountryCode = country,
        BirthDate = new(1990, 1, 1), Phone = "70000000", EmergencyContactName = "Contact",
        EmergencyContactPhone = "71111111", BloodType = "UNKNOWN"
    };

    [Theory]
    [InlineData("CI", null, "BO")]
    [InlineData("PASSPORT", "ar", "AR")]
    [InlineData("PASSPORT", "US", "US")]
    public async Task CreatesNormalizedIdentityWithoutAccount(string type, string? country, string expected)
    {
        using var c = await Admin();
        var number = "  pa" + Guid.NewGuid().ToString("N") + "  ";
        var response = await c.PostAsJsonAsync("/api/admin/patients", Request(type, country, number));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var p = (await response.Content.ReadFromJsonAsync<PatientResponse>())!;
        Assert.Equal(type, p.DocumentType); Assert.Equal(expected, p.DocumentCountryCode);
        Assert.Equal(number.Trim().ToUpperInvariant(), p.DocumentNumber);
        Assert.False(p.CanLogin); Assert.Null(p.Email);
    }
    [Theory]
    [InlineData("Passport", "US", "A123")]
    [InlineData("", "BO", "A123")]
    [InlineData("PASSPORT", null, "A123")]
    [InlineData("PASSPORT", "ZZ", "A123")]
    [InlineData("CI", "AR", "A123")]
    [InlineData("CI", "BO", " ")]
    public async Task InvalidDocumentRollsBack(string type, string? country, string number)
    {
        using var c = await Admin();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.Users.CountAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync("/api/admin/patients", Request(type, country, number))).StatusCode);
        Assert.Equal(before, await db.Users.CountAsync());
    }
    [Fact]
    public async Task DuplicateUsesTypeCountryAndNormalizedNumber()
    {
        using var c = await Admin();
        var n = "PA" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/admin/patients", Request("PASSPORT", "US", n))).StatusCode);
        var duplicate = await c.PostAsJsonAsync("/api/admin/patients", Request("PASSPORT", "US", " " + n.ToLowerInvariant() + " "));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains(IdentityDocument.ConflictMessage, await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/admin/patients", Request("PASSPORT", "AR", n))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await c.PostAsJsonAsync("/api/admin/patients", Request("CI", "BO", n))).StatusCode);
    }
    [Fact]
    public async Task EditCiToPassportSearchAndAccountRemainIndependent()
    {
        using var c = await Admin();
        var original = Request() with { Account = new() { Email = Guid.NewGuid() + "@example.test", Password = f.Password, ConfirmPassword = f.Password } };
        var created = (await (await c.PostAsJsonAsync("/api/admin/patients", original)).Content.ReadFromJsonAsync<PatientResponse>())!;
        var edit = Request("PASSPORT", "PE", "A009" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync("/api/admin/patients/" + created.Id,
            new PatientUpdateRequest { FirstName = edit.FirstName, LastName = edit.LastName, DocumentType = edit.DocumentType,
                DocumentNumber = edit.DocumentNumber, DocumentCountryCode = edit.DocumentCountryCode, BirthDate = edit.BirthDate,
                Phone = edit.Phone, EmergencyContactName = edit.EmergencyContactName, EmergencyContactPhone = edit.EmergencyContactPhone, BloodType = edit.BloodType })).StatusCode);
        var saved = (await c.GetFromJsonAsync<PatientResponse>("/api/admin/patients/" + created.Id))!;
        Assert.True(saved.CanLogin); Assert.Equal("PASSPORT", saved.DocumentType);
        Assert.Equal("PE", saved.DocumentCountryCode);
        var results = await c.GetFromJsonAsync<PatientSearchResult[]>("/api/admin/patients/search?q=" + saved.DocumentNumber![3..]);
        Assert.Contains(results!, p => p.PatientId == created.Id && p.DocumentType == "PASSPORT");
    }
    [Fact]
    public async Task MigrationCopiesExactValuesAndDatabaseEnforcesIdentity()
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDENTITY_TEST_CONNECTION")) { Database = "postgres" };
        await using var server = new NpgsqlConnection(cs.ConnectionString); await server.OpenAsync();
        var name = "document_test_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", server)) await create.ExecuteNonQueryAsync();
        try
        {
            cs.Database = name;
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs.ConnectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261016015420_AddOdontogramAndPeriodontalCharting");
            var a = Guid.NewGuid(); var b = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users" ("Id","FirstName","LastName","Ci","CanLogin","IsActive","EmailVerified","CreatedAt","UpdatedAt")
                VALUES ({a},'Migration','CI','A00123',false,true,false,now(),now()),
                       ({b},'Migration','Null',NULL,false,true,false,now(),now());
                """);
            await migrator.MigrateAsync();
            var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == a);
            Assert.Equal("CI", user.DocumentType); Assert.Equal("A00123", user.DocumentNumber); Assert.Equal("BO", user.DocumentCountryCode);
            var empty = await db.Users.AsNoTracking().SingleAsync(x => x.Id == b);
            Assert.Null(empty.DocumentType); Assert.Null(empty.DocumentNumber); Assert.Null(empty.DocumentCountryCode);
            Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name='Users' AND column_name='Ci'").SingleAsync());
            foreach (var sql in new[] {
                """UPDATE "Users" SET "DocumentType"='Passport' WHERE "DocumentNumber"='A00123'""",
                """UPDATE "Users" SET "DocumentCountryCode"='AR' WHERE "DocumentNumber"='A00123'""",
                """UPDATE "Users" SET "DocumentNumber"='a00123' WHERE "DocumentNumber"='A00123'"""
            })
                Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql))).SqlState);
            var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "Users" SET "DocumentType"='CI',"DocumentNumber"='A00123',"DocumentCountryCode"='BO' WHERE "Id"={b}"""));
            Assert.Equal("23505", duplicate.SqlState);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", server);
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Theory]
    [InlineData("doctors")]
    [InlineData("dental-assistants")]
    [InlineData("administrators")]
    [InlineData("secretaries")]
    [InlineData("cashiers")]
    public async Task ProfessionalAndStaffPassportIdentity(string route)
    {
        using var c = await Admin();
        var body = new Dictionary<string, object?>
        {
            ["firstName"] = "Passport", ["lastName"] = "Professional", ["documentType"] = "PASSPORT",
            ["documentNumber"] = "pa" + Guid.NewGuid().ToString("N"), ["documentCountryCode"] = "CL",
            ["birthDate"] = "1990-01-01", ["phone"] = "70000000",
            ["email"] = Guid.NewGuid() + "@example.test", ["password"] = f.Password, ["confirmPassword"] = f.Password
        };
        if (route == "doctors") body["professionalLicense"] = Guid.NewGuid().ToString();
        var response = await c.PostAsJsonAsync("/api/admin/" + route, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PASSPORT", json.RootElement.GetProperty("documentType").GetString());
        Assert.Equal("CL", json.RootElement.GetProperty("documentCountryCode").GetString());
        Assert.Equal(body["documentNumber"]!.ToString()!.ToUpperInvariant(), json.RootElement.GetProperty("documentNumber").GetString());
    }
}
