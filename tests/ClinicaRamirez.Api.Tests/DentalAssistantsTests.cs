using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Application.DentalAssistants;
using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.DentalAssistants;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.DentalAssistants;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;
public sealed class DentalAssistantsTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private const string Path = "/api/admin/dental-assistants";
    private Dictionary<string, object?> Body() => new() {
        ["firstName"]="Asistente", ["lastName"]="Test", ["ci"]="CI-"+Guid.NewGuid(), ["birthDate"]="1990-01-01",
        ["phone"]="70000000", ["email"]=Guid.NewGuid()+"@example.test", ["password"]=f.Password, ["confirmPassword"]=f.Password
    };
    private async Task<HttpClient> Admin() { var c=f.Client(); var login=await f.Login(c); c.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken); return c; }
    private async Task<DentalAssistantResponse> Create(HttpClient c, Dictionary<string,object?>? body=null) {
        var r=await c.PostAsJsonAsync(Path,body??Body()); Assert.Equal(HttpStatusCode.Created,r.StatusCode);
        var result=(await r.Content.ReadFromJsonAsync<DentalAssistantResponse>())!;
        Assert.EndsWith(result.Id.ToString(),r.Headers.Location!.ToString()); return result;
    }
    [Theory]
    [InlineData("firstName")][InlineData("lastName")][InlineData("ci")][InlineData("birthDate")][InlineData("phone")]
    [InlineData("email")][InlineData("password")][InlineData("confirmPassword")]
    public async Task Required(string field) { using var c=await Admin();var body=Body();body.Remove(field);Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Path,body)).StatusCode); }
    [Theory][InlineData("birthDate","2999-01-01")][InlineData("email","invalid")][InlineData("confirmPassword","DifferentPassword123!")][InlineData("password","short")]
    public async Task Invalid(string field,string value) { using var c=await Admin();var body=Body();body[field]=value;Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Path,body)).StatusCode); }
    [Theory][InlineData("userId")][InlineData("roleIds")][InlineData("professionalLicense")][InlineData("isActive")][InlineData("canLogin")]
    public async Task RejectsForeignFields(string field) { using var c=await Admin();var body=Body();body[field]="unexpected";Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Path,body)).StatusCode); }
    [Fact] public async Task CreatesUserProfileRoleCredentialsAndLogin()
    {
        using var c=await Admin();var body=Body();var profile=await Create(c,body);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user=await db.Users.SingleAsync(x=>x.Id==profile.UserId);
        Assert.True(user.CanLogin);Assert.True(user.IsActive);Assert.True(profile.IsActive);
        Assert.Equal(body["ci"]!.ToString()!.ToUpperInvariant(),user.Ci);
        Assert.Equal(PasswordVerificationResult.Success,new PasswordHasher<User>().VerifyHashedPassword(user,user.PasswordHash!,f.Password));
        Assert.True(await db.UserRoles.AnyAsync(x=>x.UserId==user.Id && x.Role.Code=="DENTAL_ASSISTANT"));
        Assert.False(await db.DoctorProfiles.AnyAsync(x=>x.UserId==user.Id));
        using var login=f.Client();var session=await f.Login(login,user.Email);Assert.Contains("DENTAL_ASSISTANT",session.Body.User.Roles);
        login.DefaultRequestHeaders.Authorization=new("Bearer",session.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,(await login.GetAsync(Path)).StatusCode);
        Assert.DoesNotContain("password",System.Text.Json.JsonSerializer.Serialize(profile).ToLowerInvariant());
    }
    [Theory][InlineData("ci")][InlineData("email")]
    public async Task GlobalDuplicatesRollBack(string field)
    {
        using var c=await Admin();var body=Body();await Create(c,body);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users=await db.Users.CountAsync();var profiles=await db.DentalAssistantProfiles.CountAsync();var roles=await db.UserRoles.CountAsync();
        var duplicate=Body();duplicate[field]=" "+body[field]!.ToString()!.ToUpperInvariant()+" ";
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Path,duplicate)).StatusCode);
        Assert.Equal(users,await db.Users.CountAsync());Assert.Equal(profiles,await db.DentalAssistantProfiles.CountAsync());Assert.Equal(roles,await db.UserRoles.CountAsync());
    }
    [Fact] public async Task UpdatePreservesUserCredentialsAndAudits()
    {
        using var c=await Admin();var profile=await Create(c);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var original=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==profile.UserId);
        var users=await db.Users.CountAsync();var body=Body();foreach(var field in new[]{"email","password","confirmPassword"})body.Remove(field);
        body["firstName"]="Editado";
        var response=await c.PutAsJsonAsync($"{Path}/{profile.Id}",body);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var updated=(await response.Content.ReadFromJsonAsync<DentalAssistantResponse>())!;
        Assert.Equal(profile.UserId,updated.UserId);Assert.Equal("Editado",updated.FirstName);Assert.Equal(users,await db.Users.CountAsync());
        var user=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==profile.UserId);Assert.Equal(original.Email,user.Email);Assert.Equal(original.PasswordHash,user.PasswordHash);
        var audits=await db.AuditLogs.Where(x=>x.EntityId==profile.Id).ToArrayAsync();
        Assert.Contains(audits,x=>x.Action=="dental_assistant_created");Assert.Contains(audits,x=>x.Action=="dental_assistant_updated");Assert.All(audits,x=>Assert.Null(x.MetadataJson));
        body["userId"]=Guid.NewGuid();Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync($"{Path}/{profile.Id}",body)).StatusCode);
    }
    [Fact] public async Task RoleProtectedWithoutBreakingOtherRoles()
    {
        using var c=await Admin();var p=await Create(c);using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var assistant=await db.Roles.SingleAsync(x=>x.Code=="DENTAL_ASSISTANT");var cashier=await db.Roles.SingleAsync(x=>x.Code=="CASHIER");
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync($"/api/admin/users/{p.UserId}/roles",new {roleIds=new[]{assistant.Id}})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await c.PutAsJsonAsync($"/api/admin/users/{p.UserId}/roles",new {roleIds=new[]{cashier.Id}})).StatusCode);
        Assert.True(await db.UserRoles.AnyAsync(x=>x.UserId==p.UserId && x.RoleId==assistant.Id));Assert.True(await db.UserRoles.AnyAsync(x=>x.UserId==p.UserId && x.RoleId==cashier.Id));
        Assert.Equal(HttpStatusCode.NoContent,(await c.PutAsJsonAsync($"/api/admin/users/{p.UserId}/roles",new {roleIds=Array.Empty<Guid>()})).StatusCode);
        Assert.True(await db.UserRoles.AnyAsync(x=>x.UserId==p.UserId && x.RoleId==assistant.Id));Assert.False(await db.UserRoles.AnyAsync(x=>x.UserId==p.UserId && x.RoleId==cashier.Id));
        Assert.Equal(HttpStatusCode.Conflict,(await c.PutAsJsonAsync($"/api/admin/roles/{assistant.Id}",new {name=assistant.Name,isActive=false})).StatusCode);
    }
    [Fact] public async Task ListDetailsAndAuthorization()
    {
        using var c=await Admin();var p=await Create(c);Assert.Equal(p,await c.GetFromJsonAsync<DentalAssistantResponse>($"{Path}/{p.Id}"));
        Assert.NotEmpty((await c.GetFromJsonAsync<DentalAssistantListResponse>(Path))!.Items);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync(Path+"?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"{Path}/{Guid.NewGuid()}")).StatusCode);
        using var anonymous=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(Path)).StatusCode);
        var login=await f.Login(anonymous,await f.CreateUser());anonymous.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,(await anonymous.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await anonymous.PostAsJsonAsync(Path,Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await anonymous.PutAsJsonAsync($"{Path}/{p.Id}",new {})).StatusCode);
    }
    [Fact] public async Task ConstraintsAndNoPersonalDuplication()
    {
        using var c=await Admin();var p=await Create(c);using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity=db.Model.FindEntityType(typeof(DentalAssistantProfile))!;
        Assert.False(entity.FindProperty("UserId")!.IsNullable);Assert.Equal(DeleteBehavior.Restrict,entity.GetForeignKeys().Single().DeleteBehavior);
        Assert.Equal(new[]{"CreatedAt","Id","IsActive","UpdatedAt","UserId"},entity.GetProperties().Select(x=>x.Name).OrderBy(x=>x).ToArray());
        db.DentalAssistantProfiles.Add(new DentalAssistantProfile {UserId=p.UserId});
        var error=await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());Assert.Equal("23505",Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
    }
    private sealed class FailProfile : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e,InterceptionResult<int> result,CancellationToken ct=default) {
            if(e.Context!.ChangeTracker.Entries<DentalAssistantProfile>().Any(x=>x.State==EntityState.Added))throw new InvalidOperationException("Simulated failure");return ValueTask.FromResult(result);
        }
    }
    [Fact] public async Task ProfileFailureRollsBackAll()
    {
        using var scope=f.Services.CreateScope();var original=scope.ServiceProvider.GetRequiredService<AppDbContext>();var users=await original.Users.CountAsync();var roles=await original.UserRoles.CountAsync();var profiles=await original.DentalAssistantProfiles.CountAsync();var audits=await original.AuditLogs.CountAsync();
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(new FailProfile()).Options);
        var service=new DentalAssistantsService(db,new IdentitySupport(db,new HttpContextAccessor()),new PasswordHasher<User>());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.Create(new DentalAssistantCreateRequest {FirstName="Test",LastName="Test",Ci=Guid.NewGuid().ToString(),BirthDate=new(1990,1,1),Phone="70000000",Email=Guid.NewGuid()+"@example.test",Password=f.Password,ConfirmPassword=f.Password}));
        Assert.Equal(users,await original.Users.CountAsync());Assert.Equal(roles,await original.UserRoles.CountAsync());Assert.Equal(profiles,await original.DentalAssistantProfiles.CountAsync());Assert.Equal(audits,await original.AuditLogs.CountAsync());
    }
}
