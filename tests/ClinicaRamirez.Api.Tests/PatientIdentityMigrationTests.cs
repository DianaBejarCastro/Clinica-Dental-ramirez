using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace ClinicaRamirez.Api.Tests;

public sealed class PatientIdentityMigrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreservaIdentidadesYAbortaConflictosSinCambiosParciales(bool conflict)
    {
        var cs = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("IDENTITY_TEST_CONNECTION")) { Database = "postgres" };
        await using var server = new NpgsqlConnection(cs.ConnectionString); await server.OpenAsync();
        var name = "identity_test_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {name}", server)) await create.ExecuteNonQueryAsync();
        cs.Database = name;
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs.ConnectionString).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261010023400_RemoveCenterGoogleMapsUrl");
        var user = Guid.NewGuid(); var linked = Guid.NewGuid(); var unlinked = Guid.NewGuid(); var role = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Roles" ("Id","Code","Name","IsSystem","IsActive","CreatedAt","UpdatedAt")
              VALUES ({role},'PATIENT','Patient',true,true,now(),now());
            INSERT INTO "Users" ("Id","FirstName","LastName","Email","PasswordHash","IsActive","EmailVerified","CreatedAt","UpdatedAt")
              VALUES ({user},'Existing','Patient','existing@example.test','existing-hash',true,false,now(),now());
            INSERT INTO "PatientProfiles" ("Id","UserId","FirstName","LastName","Ci","BirthDate","Phone","Email",
              "EmergencyContactName","EmergencyContactPhone","BloodType","IsActive","CreatedAt","UpdatedAt")
              VALUES ({linked},{user},{(conflict ? "Conflict" : "Existing")},'Patient','CI-LINKED','2000-01-01','70000000','existing@example.test','Contact','71111111','UNKNOWN',true,now(),now()),
                ({unlinked},NULL,'Clinical','Patient','CI-CLINICAL','2001-01-01','72222222',NULL,'Contact','73333333','O+',true,now(),now());
            """);
        if (conflict)
        {
            await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
            Assert.DoesNotContain(await db.Database.GetAppliedMigrationsAsync(), x => x.EndsWith("RefactorPatientIdentityToUsers"));
            Assert.Equal(2, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"PatientProfiles\"").SingleAsync());
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"Users\"").SingleAsync());
            return;
        }
        await migrator.MigrateAsync();
        var profiles = await db.PatientProfiles.Include(x => x.User).ToArrayAsync();
        Assert.Equal(2, profiles.Length);
        Assert.Equal(user, profiles.Single(x => x.Id == linked).UserId);
        var person = profiles.Single(x => x.Id == unlinked).User;
        Assert.False(person.CanLogin); Assert.Null(person.Email); Assert.Null(person.PasswordHash);
        Assert.Equal("CI-CLINICAL", person.DocumentNumber); Assert.Equal("Clinical", person.FirstName);
        var existing = profiles.Single(x => x.Id == linked).User;
        Assert.True(existing.CanLogin); Assert.Equal("existing-hash", existing.PasswordHash);
        Assert.Equal(2, await db.UserRoles.CountAsync(x => x.RoleId == role));
        Assert.Equal(2, await db.Users.CountAsync());
    }
}
