using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;
public sealed class DoctorIdentityTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private Dictionary<string, object?> Body() => new() {
        ["firstName"]="Doctor", ["lastName"]="Test", ["documentType"]="CI", ["documentCountryCode"]="BO", ["documentNumber"]=Guid.NewGuid().ToString(), ["birthDate"]="1990-01-01",
        ["phone"]="70000000", ["professionalLicense"]=Guid.NewGuid().ToString(), ["email"]=Guid.NewGuid()+"@example.test",
        ["password"]=f.Password, ["confirmPassword"]=f.Password
    };
    private async Task<HttpClient> Admin() { var c=f.Client(); var s=await f.Login(c); c.DefaultRequestHeaders.Authorization=new("Bearer",s.Body.AccessToken); return c; }
    [Theory]
    [InlineData("firstName")][InlineData("lastName")][InlineData("documentNumber")][InlineData("birthDate")][InlineData("phone")]
    [InlineData("professionalLicense")][InlineData("email")][InlineData("password")][InlineData("confirmPassword")]
    public async Task RequiredFields(string field) { using var c=await Admin(); var body=Body(); body.Remove(field); Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/admin/doctors",body)).StatusCode); }
    [Theory][InlineData("birthDate","2999-01-01")][InlineData("confirmPassword","DoesNotMatch123!")][InlineData("email","invalid")]
    public async Task InvalidFields(string field,string value) { using var c=await Admin();var body=Body();body[field]=value;Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/admin/doctors",body)).StatusCode); }
    [Fact] public async Task CreateCredentialsProfileRoleAndLogin()
    {
        using var c=await Admin();var body=Body();var response=await c.PostAsJsonAsync("/api/admin/doctors",body);Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var doctor=(await response.Content.ReadFromJsonAsync<DoctorResponse>())!;
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var user=await db.Users.SingleAsync(x=>x.Id==doctor.UserId);
        Assert.True(user.CanLogin);Assert.True(user.IsActive);Assert.Equal(body["documentNumber"]!.ToString()!.ToUpperInvariant(),user.DocumentNumber);
        Assert.Equal(PasswordVerificationResult.Success,new PasswordHasher<User>().VerifyHashedPassword(user,user.PasswordHash!,f.Password));
        Assert.True(await db.UserRoles.AnyAsync(x=>x.UserId==user.Id && x.Role.Code=="DOCTOR"));
        using var login=f.Client();Assert.Contains("DOCTOR",(await f.Login(login,user.Email)).Body.User.Roles);
        var role=await db.Roles.SingleAsync(x=>x.Code=="DOCTOR");
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync($"/api/admin/users/{user.Id}/roles",new {roleIds=new[]{role.Id}})).StatusCode);
        var usersBefore=await db.Users.CountAsync(); var profilesBefore=await db.DoctorProfiles.CountAsync(); var rolesBefore=await db.UserRoles.CountAsync();
        var duplicate=Body();duplicate["documentNumber"]=" " + body["documentNumber"]!.ToString()!.ToLowerInvariant() + " ";
        var conflict=await c.PostAsJsonAsync("/api/admin/doctors",duplicate);
        Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);
        Assert.Contains(ClinicaRamirez.Api.Application.Identity.IdentityDocument.ConflictMessage,await conflict.Content.ReadAsStringAsync());
        Assert.Equal(usersBefore,await db.Users.CountAsync()); Assert.Equal(profilesBefore,await db.DoctorProfiles.CountAsync()); Assert.Equal(rolesBefore,await db.UserRoles.CountAsync());
        var patientsBefore=await db.PatientProfiles.CountAsync();
        var patientConflict=await c.PostAsJsonAsync("/api/admin/patients",new { firstName="Test",lastName="Patient",documentType="CI", documentCountryCode="BO", documentNumber=body["documentNumber"],birthDate="1990-01-01",phone="70000000",emergencyContactName="Test",emergencyContactPhone="71111111",bloodType="UNKNOWN" });
        Assert.Equal(HttpStatusCode.Conflict,patientConflict.StatusCode);
        Assert.Contains(ClinicaRamirez.Api.Application.Identity.IdentityDocument.ConflictMessage,await patientConflict.Content.ReadAsStringAsync());
        Assert.Equal(patientsBefore,await db.PatientProfiles.CountAsync()); Assert.Equal(usersBefore,await db.Users.CountAsync()); Assert.Equal(rolesBefore,await db.UserRoles.CountAsync());
        duplicate=Body();duplicate["email"]=body["email"];Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync("/api/admin/doctors",duplicate)).StatusCode);
        Assert.Null(db.Model.FindEntityType(typeof(DoctorProfile))!.FindProperty("DocumentNumber"));
        Assert.True(db.Model.FindEntityType(typeof(DoctorProfile))!.GetIndexes().Single(x=>x.Properties.SingleOrDefault()?.Name=="UserId").IsUnique);
    }
    private sealed class FailProfile : SaveChangesInterceptor {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,InterceptionResult<int> result,CancellationToken ct=default) {
            if(e.Context!.ChangeTracker.Entries<DoctorProfile>().Any(x=>x.State==EntityState.Added)) throw new InvalidOperationException("Simulated failure");
            return ValueTask.FromResult(result);
        }
    }
    [Fact] public async Task ProfileFailureRollsBackEverything()
    {
        using var scope=f.Services.CreateScope();var original=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users=await original.Users.CountAsync();var roles=await original.UserRoles.CountAsync();var profiles=await original.DoctorProfiles.CountAsync();
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(new FailProfile()).Options);
        var service=new ClinicAdministrationService(db,new IdentitySupport(db,new HttpContextAccessor()),new PasswordHasher<User>());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CreateDoctor(new DoctorCreateRequest {FirstName="Test",LastName="Test",DocumentType="CI", DocumentCountryCode="BO", DocumentNumber=Guid.NewGuid().ToString(),BirthDate=new(1990,1,1),Phone="70000000",ProfessionalLicense=Guid.NewGuid().ToString(),Email=Guid.NewGuid()+"@example.test",Password=f.Password,ConfirmPassword=f.Password}));
        Assert.Equal(users,await original.Users.CountAsync());Assert.Equal(roles,await original.UserRoles.CountAsync());Assert.Equal(profiles,await original.DoctorProfiles.CountAsync());
    }
}
