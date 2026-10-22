using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;
public sealed class PatientSearchTests(IdentityFixture f):IClassFixture<IdentityFixture>
{
    private async Task<HttpClient> Admin(){var c=f.Client();var s=await f.Login(c);c.DefaultRequestHeaders.Authorization=new("Bearer",s.Body.AccessToken);return c;}
    private async Task<PatientProfile> Patient(bool active=true) {
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var p=new PatientProfile{User=new User{FirstName="Andrea",LastName="Bejar",DocumentType="CI", DocumentCountryCode="BO", DocumentNumber="SEARCH-"+Guid.NewGuid().ToString().ToUpperInvariant(),Phone="71234567",Email=Guid.NewGuid()+"@gmail.test",CanLogin=false,BirthDate=new(1990,1,1)},EmergencyContactName="Private",EmergencyContactPhone="70000001",BloodType="UNKNOWN",Notes="Private clinical note",IsActive=active};
        db.PatientProfiles.Add(p);var role=await db.Roles.SingleAsync(x=>x.Code=="PATIENT");db.UserRoles.Add(new UserRole{User=p.User,RoleId=role.Id});await db.SaveChangesAsync();return p;
    }
    private static Task<PatientSearchResult[]?> Search(HttpClient c,string q,string limit="20") => c.GetFromJsonAsync<PatientSearchResult[]>($"/api/admin/patients/search?q={Uri.EscapeDataString(q)}&limit={limit}");
    [Theory][InlineData("")][InlineData(" ")][InlineData("a")][InlineData(" a ")]
    public async Task ShortQueriesAreEmpty(string q){using var c=await Admin();await Patient();Assert.Empty((await Search(c,q))!);}
    [Theory][InlineData("documentNumber")][InlineData("partial-documentNumber")][InlineData("first")][InlineData("last")][InlineData("email")][InlineData("phone")][InlineData("case")][InlineData("spaces")]
    public async Task FindsPartialPersonalFields(string field) {
        using var c=await Admin();var p=await Patient();var q=field switch{"documentNumber"=>p.User.DocumentNumber!,"partial-documentNumber"=>p.User.DocumentNumber![7..20],"first"=>"andr","last"=>"bejar","email"=>"gmail","phone"=>"7123","case"=>"aNdReA",_=>"  Andrea   Bejar  "};
        if(field is not ("documentNumber" or "partial-documentNumber")) q += " " + p.User.DocumentNumber;
        Assert.Contains((await Search(c,q))!,r=>r.PatientId==p.Id&&r.UserId==p.UserId);
    }
    [Fact] public async Task ActiveProfilesOnlyAndNoLoginRequirement(){using var c=await Admin();var p=await Patient();var inactive=await Patient(false);Assert.Single((await Search(c,p.User.DocumentNumber!))!);Assert.Empty((await Search(c,inactive.User.DocumentNumber!))!);Assert.False(p.User.CanLogin);}
    [Fact] public async Task UsersWithoutPatientProfileAreExcluded(){using var c=await Admin();var email=await f.CreateUser();Assert.Empty((await Search(c,email))!);}
    [Theory][InlineData("10",10)][InlineData("3",3)][InlineData("999",20)]
    public async Task LimitsAreEnforced(string limit,int expected){using var c=await Admin();for(var i=0;i<22;i++)await Patient();Assert.Equal(expected,(await Search(c,"Andrea",limit))!.Length);}
    [Fact] public async Task MinimalDtoAndCorrectId(){using var c=await Admin();var p=await Patient();var response=await c.GetAsync($"/api/admin/patients/search?q={p.User.DocumentNumber}");using var json=System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());var item=json.RootElement[0];Assert.Equal(p.Id,item.GetProperty("patientId").GetGuid());Assert.NotEqual(p.UserId,p.Id);Assert.Equal(new[]{"documentCountryCode","documentNumber","documentType","email","firstName","lastName","patientId","phone","userId"},item.EnumerateObject().Select(x=>x.Name).Order().ToArray());}
    [Theory][InlineData("%_")][InlineData("' OR 1=1 --")]
    public async Task SearchCharactersAreLiteral(string q){using var c=await Admin();await Patient();Assert.Empty((await Search(c,q))!);}
    [Fact] public async Task ExactCiRanksFirst(){using var c=await Admin();var p=await Patient();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var other=await Patient();await db.Users.Where(x=>x.Id==other.UserId).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.DocumentNumber,p.User.DocumentNumber+"-MORE"));Assert.Equal(p.Id,(await Search(c,p.User.DocumentNumber!))![0].PatientId);}
    [Fact] public async Task AuthorizationAndBounds(){using var c=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/admin/patients/search?q=an")).StatusCode);var s=await f.Login(c,await f.CreateUser());c.DefaultRequestHeaders.Authorization=new("Bearer",s.Body.AccessToken);Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/admin/patients/search?q=an")).StatusCode);using var admin=await Admin();Assert.Equal(HttpStatusCode.BadRequest,(await admin.GetAsync("/api/admin/patients/search?q="+new string('a',201))).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await admin.GetAsync("/api/admin/patients/search?q=an&limit=0")).StatusCode);}
}
