using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using ClinicaRamirez.Api.Application.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;

public sealed class StaffAdministrationTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private Dictionary<string,object?> Body() => new() {
        ["firstName"]="Personal", ["lastName"]="Test", ["documentType"]="CI", ["documentCountryCode"]="BO", ["documentNumber"]="STAFF-"+Guid.NewGuid(), ["birthDate"]="1990-01-01",
        ["phone"]="70000000", ["email"]=Guid.NewGuid()+"@example.test", ["password"]=f.Password, ["confirmPassword"]=f.Password
    };
    private async Task<HttpClient> Admin() { var c=f.Client();var session=await f.Login(c);c.DefaultRequestHeaders.Authorization=new("Bearer",session.Body.AccessToken);return c; }
    private async Task<UserResponse> Create(HttpClient c,string path="secretaries",Dictionary<string,object?>? body=null) {
        var response=await c.PostAsJsonAsync("/api/admin/"+path,body??Body());Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }
    [Theory][InlineData("administrators","ADMINISTRATOR")][InlineData("cashiers","CASHIER")][InlineData("secretaries","SECRETARY")]
    public async Task CreatesAndListsOnlyMatchingRole(string path,string code)
    {
        using var c=await Admin();var body=Body();var user=await Create(c,path,body);
        Assert.True(user.CanLogin);Assert.True(user.IsActive);Assert.Equal(code,Assert.Single(user.Roles).Code);
        var list=(await c.GetFromJsonAsync<UserListResponse>($"/api/admin/{path}?q={user.DocumentNumber}"))!;
        Assert.Equal(user.Id,Assert.Single(list.Items).Id);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved=await db.Users.SingleAsync(x=>x.Id==user.Id);
        Assert.Equal(PasswordVerificationResult.Success,new PasswordHasher<User>().VerifyHashedPassword(saved,saved.PasswordHash!,f.Password));
        Assert.False(await db.DoctorProfiles.AnyAsync(x=>x.UserId==user.Id));Assert.False(await db.PatientProfiles.AnyAsync(x=>x.UserId==user.Id));Assert.False(await db.DentalAssistantProfiles.AnyAsync(x=>x.UserId==user.Id));
        Assert.True(await db.AuditLogs.AnyAsync(x=>x.EntityId==user.Id && x.Action==code.ToLower()+"_created"));
        using var login=f.Client();Assert.Contains(code,(await f.Login(login,user.Email)).Body.User.Roles);
    }
    [Theory][InlineData("firstName")][InlineData("lastName")][InlineData("documentNumber")][InlineData("birthDate")][InlineData("phone")][InlineData("email")][InlineData("password")][InlineData("confirmPassword")]
    public async Task RequiredFields(string field) { using var c=await Admin();var body=Body();body.Remove(field);Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/admin/cashiers",body)).StatusCode); }
    [Theory][InlineData("birthDate","2999-01-01")][InlineData("confirmPassword","different-password")][InlineData("roleIds","ADMINISTRATOR")]
    public async Task InvalidOrArbitraryRoleRejected(string field,string value) { using var c=await Admin();var body=Body();body[field]=value;Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/admin/cashiers",body)).StatusCode); }
    [Theory][InlineData("documentNumber")][InlineData("email")]
    public async Task DuplicateDoesNotCreatePartialRows(string field)
    {
        using var c=await Admin();var body=Body();await Create(c,body:body);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var users=await db.Users.CountAsync();var grants=await db.UserRoles.CountAsync();
        var duplicate=Body();duplicate[field]=body[field]!.ToString()!.ToUpperInvariant();
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync("/api/admin/cashiers",duplicate)).StatusCode);
        Assert.Equal(users,await db.Users.CountAsync());Assert.Equal(grants,await db.UserRoles.CountAsync());
    }
    [Theory][InlineData("administrators")][InlineData("secretaries")]
    public async Task SimpleRolesAreMultiRoleAndRemovable(string path)
    {
        using var c=await Admin();var user=await Create(c,path);
        var roles=(await c.GetFromJsonAsync<RoleResponse[]>("/api/admin/roles"))!;var cashier=roles.Single(x=>x.Code=="CASHIER");
        Assert.Equal(HttpStatusCode.NoContent,(await c.PutAsJsonAsync($"/api/admin/users/{user.Id}/roles",new{roleIds=user.Roles.Select(x=>x.Id).Append(cashier.Id)})).StatusCode);
        var updated=(await c.GetFromJsonAsync<UserResponse>($"/api/admin/users/{user.Id}"))!;Assert.Equal(2,updated.Roles.Length);
        Assert.Equal(HttpStatusCode.NoContent,(await c.PutAsJsonAsync($"/api/admin/users/{user.Id}/roles",new{roleIds=new[]{cashier.Id}})).StatusCode);
        Assert.Equal("CASHIER",Assert.Single((await c.GetFromJsonAsync<UserResponse>($"/api/admin/users/{user.Id}"))!.Roles).Code);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.AuditLogs.AnyAsync(x=>x.EntityId==user.Id&&x.Action=="user_role_added"));Assert.True(await db.AuditLogs.AnyAsync(x=>x.EntityId==user.Id&&x.Action=="user_role_removed"));
    }
    [Fact] public async Task PatientAddsDoctorAndAssistantWithSameIdentityCredentialsAndPreviousRoles()
    {
        using var c=await Admin();var body=Body();var person=await Create(c,body:body);
        var patient=await c.PostAsJsonAsync($"/api/admin/users/{person.Id}/patient-profile",new{emergencyContactName="Contacto",emergencyContactPhone="70000001",bloodType="UNKNOWN"});Assert.Equal(HttpStatusCode.OK,patient.StatusCode);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var users=await db.Users.CountAsync();var original=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==person.Id);
        var doctor=await c.PostAsJsonAsync($"/api/admin/users/{person.Id}/doctor-profile",new{professionalLicense="STAFF-"+Guid.NewGuid()});Assert.Equal(HttpStatusCode.OK,doctor.StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync($"/api/admin/users/{person.Id}/dental-assistant-profile",new{})).StatusCode);
        var updated=(await c.GetFromJsonAsync<UserResponse>($"/api/admin/users/{person.Id}"))!;
        Assert.Equal(person.Id,updated.Id);Assert.Equal(new[]{"DENTAL_ASSISTANT","DOCTOR","PATIENT","SECRETARY"},updated.Roles.Select(x=>x.Code).Order().ToArray());
        Assert.Equal(users,await db.Users.CountAsync());Assert.Single(await db.DoctorProfiles.Where(x=>x.UserId==person.Id).ToListAsync());Assert.Single(await db.PatientProfiles.Where(x=>x.UserId==person.Id).ToListAsync());Assert.Single(await db.DentalAssistantProfiles.Where(x=>x.UserId==person.Id).ToListAsync());
        var saved=await db.Users.AsNoTracking().SingleAsync(x=>x.Id==person.Id);Assert.Equal(original.PasswordHash,saved.PasswordHash);Assert.Equal(original.Email,saved.Email);Assert.Equal(original.DocumentNumber,saved.DocumentNumber);
        Assert.Equal(HttpStatusCode.NoContent,(await c.PutAsJsonAsync($"/api/admin/users/{person.Id}/roles",new{roleIds=Array.Empty<Guid>()})).StatusCode);
        var preserved=(await c.GetFromJsonAsync<UserResponse>($"/api/admin/users/{person.Id}"))!;
        Assert.Equal(new[]{"DENTAL_ASSISTANT","DOCTOR","PATIENT"},preserved.Roles.Select(x=>x.Code).Order().ToArray());
        foreach(var action in new[]{"patient_created","doctor_created","dental_assistant_created"}) Assert.True(await db.AuditLogs.AnyAsync(x=>x.Action==action));
    }
    [Theory][InlineData("doctor-profile")][InlineData("patient-profile")][InlineData("dental-assistant-profile")]
    public async Task DuplicateProfileRejectedAndNoNewUser(string path)
    {
        using var c=await Admin();var user=await Create(c);object body=path switch {"doctor-profile"=>new{professionalLicense="LIC-"+Guid.NewGuid()},"patient-profile"=>new{emergencyContactName="Contacto",emergencyContactPhone="70000001",bloodType="O+"},_=>new{}};
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync($"/api/admin/users/{user.Id}/{path}",body)).StatusCode);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var users=await db.Users.CountAsync();var grants=await db.UserRoles.CountAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/admin/users/{user.Id}/{path}",body)).StatusCode);
        Assert.Equal(users,await db.Users.CountAsync());Assert.Equal(grants,await db.UserRoles.CountAsync());
    }
    [Fact] public async Task MissingPersonalDataOrCredentialsBlocksProfessionalRole()
    {
        using var c=await Admin();var email=await f.CreateUser();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var user=await db.Users.SingleAsync(x=>x.Email==email);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/admin/users/{user.Id}/dental-assistant-profile",new{})).StatusCode);
        user.DocumentType="CI";user.DocumentCountryCode="BO";user.DocumentNumber="NO-LOGIN-"+Guid.NewGuid();user.BirthDate=new(2000,1,1);user.Phone="70000000";user.CanLogin=false;user.PasswordHash=null;await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/admin/users/{user.Id}/doctor-profile",new{professionalLicense="NO-LOGIN-"+Guid.NewGuid()})).StatusCode);
        Assert.False(await db.DoctorProfiles.AnyAsync(x=>x.UserId==user.Id));Assert.False(await db.DentalAssistantProfiles.AnyAsync(x=>x.UserId==user.Id));
    }
    [Fact] public async Task PatientWithoutAccountCanReceivePatientProfileAndAppearsInGlobalSearch()
    {
        using var c=await Admin();var email=await f.CreateUser();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var user=await db.Users.SingleAsync(x=>x.Email==email);
        user.DocumentType="CI";user.DocumentCountryCode="BO";user.DocumentNumber="CLINICAL-"+Guid.NewGuid();user.BirthDate=new(2000,1,1);user.Phone="70000000";user.CanLogin=false;user.PasswordHash=null;await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync($"/api/admin/users/{user.Id}/patient-profile",new{emergencyContactName="Contacto",emergencyContactPhone="70000001",bloodType="UNKNOWN"})).StatusCode);
        var list=(await c.GetFromJsonAsync<UserListResponse>($"/api/admin/users?q={user.DocumentNumber}"))!;Assert.Equal(user.Id,Assert.Single(list.Items).Id);Assert.False(list.Items[0].CanLogin);
    }
    [Theory][InlineData("DOCTOR")][InlineData("PATIENT")][InlineData("DENTAL_ASSISTANT")]
    public async Task DomainRolesRejectGenericGrant(string code)
    {
        using var c=await Admin();var user=await Create(c);var roles=(await c.GetFromJsonAsync<RoleResponse[]>("/api/admin/roles"))!;
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync($"/api/admin/users/{user.Id}/roles",new{roleIds=new[]{roles.Single(x=>x.Code==code).Id}})).StatusCode);
    }
    [Theory][InlineData("administrators")][InlineData("cashiers")][InlineData("secretaries")]
    public async Task UnauthorizedAndForbidden(string path)
    {
        using var c=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await c.PostAsJsonAsync("/api/admin/"+path,Body())).StatusCode);
        var login=await f.Login(c,await f.CreateUser());c.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/admin/"+path)).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync("/api/admin/"+path,Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync($"/api/admin/users/{login.Body.User.Id}/dental-assistant-profile",new{})).StatusCode);
    }
    [Fact] public async Task SeedDoesNotRecreateSyntheticRole()
    {
        using var scope=f.Services.CreateScope();await scope.ServiceProvider.GetRequiredService<IdentitySeed>().Run();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Roles.AnyAsync(x=>x.Code=="ROL_PRUEBA"||x.Name=="Rol de prueba"));
    }
    [Fact] public async Task DuplicateLicenseAndConcurrentProfileRequestsAreControlled()
    {
        using var c=await Admin();var first=await Create(c);var second=await Create(c);var license="MULTI-"+Guid.NewGuid();
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync($"/api/admin/users/{first.Id}/doctor-profile",new{professionalLicense=license})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync($"/api/admin/users/{second.Id}/doctor-profile",new{professionalLicense=license})).StatusCode);
        var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>c.PostAsJsonAsync($"/api/admin/users/{second.Id}/dental-assistant-profile",new{})));
        Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.DoctorProfiles.AnyAsync(x=>x.UserId==second.Id));Assert.False(await db.UserRoles.AnyAsync(x=>x.UserId==second.Id&&x.Role.Code=="DOCTOR"));
        Assert.Equal(1,await db.DentalAssistantProfiles.CountAsync(x=>x.UserId==second.Id));
    }
    private sealed class FailAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e,int result,CancellationToken ct=default)
            => throw new InvalidOperationException("Simulated failure after SQL, before commit");
    }
    [Theory][InlineData("STAFF")][InlineData("PATIENT")][InlineData("DOCTOR")][InlineData("DENTAL_ASSISTANT")]
    public async Task TransactionRollsBackAfterSaving(string code)
    {
        using var c=await Admin();var existing=await Create(c);
        using var scope=f.Services.CreateScope();var original=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users=await original.Users.CountAsync();var roles=await original.UserRoles.CountAsync();var profiles=await original.DentalAssistantProfiles.CountAsync();var doctors=await original.DoctorProfiles.CountAsync();var patients=await original.PatientProfiles.CountAsync();var audits=await original.AuditLogs.CountAsync();
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(new FailAfterSave()).Options);
        var service=new StaffAdministrationService(db,new IdentitySupport(db,new HttpContextAccessor()),new PasswordHasher<User>());
        if(code!="STAFF") {
            object request=code switch {"DOCTOR"=>new AddDoctorProfileRequest{ProfessionalLicense="ROLLBACK-"+Guid.NewGuid()},"PATIENT"=>new AddPatientProfileRequest{EmergencyContactName="Test",EmergencyContactPhone="70000001",BloodType="UNKNOWN"},_=>new AddDentalAssistantProfileRequest()};
            await Assert.ThrowsAsync<InvalidOperationException>(()=>service.AddProfile(existing.Id,code,request));
        }
        else await Assert.ThrowsAsync<InvalidOperationException>(()=>service.Create("CASHIER",new StaffCreateRequest{FirstName="Test",LastName="Test",DocumentType="CI", DocumentCountryCode="BO", DocumentNumber="ROLLBACK-"+Guid.NewGuid(),BirthDate=new(1990,1,1),Phone="70000000",Email=Guid.NewGuid()+"@example.test",Password=f.Password,ConfirmPassword=f.Password}));
        Assert.Equal(users,await original.Users.CountAsync());Assert.Equal(roles,await original.UserRoles.CountAsync());Assert.Equal(profiles,await original.DentalAssistantProfiles.CountAsync());Assert.Equal(doctors,await original.DoctorProfiles.CountAsync());Assert.Equal(patients,await original.PatientProfiles.CountAsync());Assert.Equal(audits,await original.AuditLogs.CountAsync());
    }
    [Fact] public async Task UserCreationPermissionAloneCannotGrantAnyRole()
    {
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var email=await f.CreateUser();var user=await db.Users.SingleAsync(x=>x.Email==email);
        var role=new Role{Code="CREATOR_"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Name="Limited creator "+Guid.NewGuid()};db.Roles.Add(role);
        var permission=await db.Permissions.SingleAsync(x=>x.Code=="users.create");db.RolePermissions.Add(new RolePermission{Role=role,PermissionId=permission.Id});db.UserRoles.Add(new UserRole{UserId=user.Id,Role=role});await db.SaveChangesAsync();
        using var c=f.Client();var session=await f.Login(c,email);c.DefaultRequestHeaders.Authorization=new("Bearer",session.Body.AccessToken);
        foreach(var path in new[]{"administrators","cashiers","secretaries"}) Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync("/api/admin/"+path,Body())).StatusCode);
    }
    [Fact] public async Task IdentityUpdateHasGlobalCiAndBirthDateValidation()
    {
        using var c=await Admin();var first=await Create(c);var second=await Create(c);
        var duplicate=await c.PutAsJsonAsync($"/api/admin/users/{second.Id}",new{second.FirstName,second.LastName,second.Email,second.Phone,documentType="CI", documentCountryCode="BO", documentNumber=first.DocumentNumber});Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync($"/api/admin/users/{second.Id}",new{second.FirstName,second.LastName,second.Email,second.Phone,birthDate="2999-01-01"})).StatusCode);
        Assert.Equal(second.DocumentNumber,(await c.GetFromJsonAsync<UserResponse>($"/api/admin/users/{second.Id}"))!.DocumentNumber);
    }
    [Theory][InlineData("DOCTOR","doctor-profile")][InlineData("PATIENT","patient-profile")][InlineData("DENTAL_ASSISTANT","dental-assistant-profile")]
    public async Task LegacyGrantCanExplicitlyCompleteMissingProfileWithoutDuplicatingRole(string code,string path)
    {
        using var c=await Admin();var user=await Create(c);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var role=await db.Roles.SingleAsync(x=>x.Code==code);
        db.UserRoles.Add(new UserRole{UserId=user.Id,RoleId=role.Id});await db.SaveChangesAsync();
        var before=(await c.GetFromJsonAsync<UserResponse>($"/api/admin/users/{user.Id}"))!;Assert.False(before.HasDoctorProfile);Assert.False(before.HasPatientProfile);Assert.False(before.HasDentalAssistantProfile);
        var users=await db.Users.CountAsync();var grants=await db.UserRoles.CountAsync();
        object body=path switch {"doctor-profile"=>new{professionalLicense="LEGACY-"+Guid.NewGuid()},"patient-profile"=>new{emergencyContactName="Contacto",emergencyContactPhone="70000001",bloodType="UNKNOWN"},_=>new{}};
        var response=await c.PostAsJsonAsync($"/api/admin/users/{user.Id}/{path}",body);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var result=(await response.Content.ReadFromJsonAsync<UserResponse>())!;Assert.True(result.HasDoctorProfile||result.HasPatientProfile||result.HasDentalAssistantProfile);
        Assert.Equal(users,await db.Users.CountAsync());Assert.Equal(grants,await db.UserRoles.CountAsync());
    }
    [Theory][InlineData("DOCTOR","doctors")][InlineData("PATIENT","patients")]
    public async Task ProtectedProfileDeletionRemovesOnlyItsDomainGrant(string code,string kind)
    {
        using var c=await Admin();var user=await Create(c);
        var path=code=="DOCTOR"?"doctor-profile":"patient-profile";
        object body=code=="DOCTOR"?(object)new{professionalLicense="DELETE-"+Guid.NewGuid()}:new{emergencyContactName="Test",emergencyContactPhone="70000001",bloodType="UNKNOWN"};
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync($"/api/admin/users/{user.Id}/{path}",body)).StatusCode);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id=code=="DOCTOR"?await db.DoctorProfiles.Where(x=>x.UserId==user.Id).Select(x=>x.Id).SingleAsync():await db.PatientProfiles.Where(x=>x.UserId==user.Id).Select(x=>x.Id).SingleAsync();
        Assert.Equal(HttpStatusCode.NoContent,(await c.PatchAsJsonAsync($"/api/admin/{kind}/{id}/status",new{isActive=false})).StatusCode);
        using var request=new HttpRequestMessage(HttpMethod.Delete,$"/api/admin/deletion/{kind}/{id}"){Content=JsonContent.Create(new{password=f.Password})};
        Assert.Equal(HttpStatusCode.NoContent,(await c.SendAsync(request)).StatusCode);
        Assert.True(await db.Users.AnyAsync(x=>x.Id==user.Id));
        Assert.True(await db.UserRoles.AnyAsync(x=>x.UserId==user.Id&&x.Role.Code=="SECRETARY"));
        Assert.False(await db.UserRoles.AnyAsync(x=>x.UserId==user.Id&&x.Role.Code==code));
        Assert.False(await db.DoctorProfiles.AnyAsync(x=>x.UserId==user.Id));Assert.False(await db.PatientProfiles.AnyAsync(x=>x.UserId==user.Id));
        Assert.True(await db.AuditLogs.AnyAsync(x=>x.EntityId==user.Id&&x.Action=="user_role_removed"));
    }
}
