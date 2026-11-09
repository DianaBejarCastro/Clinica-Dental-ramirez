using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClinicaRamirez.Api.Tests;
public sealed class FeaturedDoctorsTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private const string Root = "/api/admin/clinic/team";
    private async Task<HttpClient> Admin() { var c = f.Client(); var login = await f.Login(c); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken); return c; }
    private async Task<DoctorProfile> Doctor(bool activeUser = true, bool activeDoctor = true, bool assigned = true) {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var d = new DoctorProfile { User = new User { FirstName = "Real", LastName = "Doctor", Email = Guid.NewGuid()+"@example.test", PasswordHash = "unused", IsActive = activeUser }, ProfessionalLicense = "T"+Guid.NewGuid().ToString("N").ToUpperInvariant(), IsActive = activeDoctor };
        db.DoctorProfiles.Add(d);
        if (assigned) { var center = new Center { ClinicId = Clinic.SingletonId, Name = "Test", Code = "TEST_"+Guid.NewGuid().ToString("N").ToUpperInvariant(), Address = "Test" }; db.Centers.Add(center); db.DoctorCenters.Add(new() { Doctor = d, Center = center }); }
        await db.SaveChangesAsync(); return d;
    }
    private static async Task<Guid> Add(HttpClient c, DoctorProfile d, int order = 0) {
        var r = await c.PostAsJsonAsync(Root, new FeaturedDoctorRequest(d.Id, order)); Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); return body.GetProperty("id").GetGuid();
    }
    [Theory][InlineData(true,true,true,true)][InlineData(false,true,true,false)][InlineData(true,false,true,false)][InlineData(true,true,false,false)]
    public async Task AvailableFiltersAndAddValidation(bool user, bool doctor, bool assigned, bool expected) {
        var d=await Doctor(user,doctor,assigned); using var c=await Admin();
        var list=(await c.GetFromJsonAsync<AvailableDoctorResponse[]>(Root+"/available"))!;
        Assert.Equal(expected,list.Any(x=>x.DoctorProfileId==d.Id));
        var r=await c.PostAsJsonAsync(Root,new FeaturedDoctorRequest(d.Id,0)); Assert.Equal(expected?HttpStatusCode.Created:HttpStatusCode.BadRequest,r.StatusCode);
    }
    [Fact] public async Task DuplicateRejectedAndRemovalPreservesDoctorAndReusesRelationship() {
        var d=await Doctor(); using var c=await Admin(); var id=await Add(c,d);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsJsonAsync(Root,new FeaturedDoctorRequest(d.Id,0))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await c.DeleteAsync($"{Root}/{id}")).StatusCode);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True((await db.DoctorProfiles.FindAsync(d.Id))!.IsActive); Assert.True((await db.Users.FindAsync(d.UserId))!.IsActive);
        Assert.False((await db.ClinicFeaturedDoctors.FindAsync(id))!.IsActive); Assert.Equal(id,await Add(c,d));
        Assert.Equal(1,await db.ClinicFeaturedDoctors.CountAsync(x=>x.DoctorProfileId==d.Id));
    }
    [Fact] public async Task InvalidDoctorAndOrderRejected() {
        using var c=await Admin(); Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Root,new FeaturedDoctorRequest(Guid.NewGuid(),0))).StatusCode);
        var d=await Doctor();Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync(Root,new FeaturedDoctorRequest(d.Id,-1))).StatusCode);
    }
    [Theory][InlineData(true)][InlineData(false)] public async Task DeactivatedDoctorOrUserDisappears(bool deactivateUser) {
        var d=await Doctor();using var c=await Admin();var id=await Add(c,d);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if(deactivateUser)(await db.Users.FindAsync(d.UserId))!.IsActive=false;else(await db.DoctorProfiles.FindAsync(d.Id))!.IsActive=false;await db.SaveChangesAsync();
        var list=(await c.GetFromJsonAsync<PublicTeamResponse[]>("/api/clinic/team"))!;Assert.DoesNotContain(list,x=>x.Id==id);
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/clinic/team/{id}/photo")).StatusCode);
    }
    [Fact] public async Task PublicSelectedOnlyOrderedAndNamesResolvedFromUsers() {
        var a=await Doctor();var b=await Doctor();var unselected=await Doctor();using var c=await Admin();var first=await Add(c,a,50);var second=await Add(c,b,40);
        Assert.Equal(HttpStatusCode.NoContent,(await c.PutAsJsonAsync($"{Root}/{first}/order",new {displayOrder=30})).StatusCode);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();(await db.Users.FindAsync(a.UserId))!.FirstName="Changed";await db.SaveChangesAsync();
        var rows=(await c.GetFromJsonAsync<PublicTeamResponse[]>("/api/clinic/team"))!;Assert.Equal("Changed",rows.Single(x=>x.Id==first).FirstName);
        Assert.True(Array.FindIndex(rows,x=>x.Id==first)<Array.FindIndex(rows,x=>x.Id==second));
        Assert.DoesNotContain((await c.GetFromJsonAsync<FeaturedDoctorResponse[]>(Root))!,x=>x.DoctorProfileId==unselected.Id);
    }
    [Theory][InlineData(true)][InlineData(false)] public async Task DatabaseRejectsOrphanClinicOrDoctor(bool clinic) {
        var d=await Doctor();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ClinicFeaturedDoctors.Add(new(){ClinicId=clinic?Guid.NewGuid():Clinic.SingletonId,DoctorProfileId=clinic?d.Id:Guid.NewGuid()});
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
    }
    [Fact] public async Task UniqueAndRestrictConstraints() {
        var d=await Doctor();using var c=await Admin();await Add(c,d);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var model=db.Model.FindEntityType(typeof(ClinicFeaturedDoctor))!;Assert.Equal(2,model.GetForeignKeys().Count());Assert.All(model.GetForeignKeys(),x=>Assert.Equal(DeleteBehavior.Restrict,x.DeleteBehavior));
        db.ClinicFeaturedDoctors.Add(new(){ClinicId=Clinic.SingletonId,DoctorProfileId=d.Id});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
    }
    [Fact] public async Task PhotoUsesExistingStorageAndBecomesPrivateAfterRemoval() {
        var d=await Doctor();using var c=await Admin();var id=await Add(c,d);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        byte[] bytes=[1,2,3]; db.UserProfilePhotos.Add(new(){UserId=d.UserId,ImageData=bytes,MimeType="image/webp",Width=1080,Height=1350,FileSizeBytes=3});await db.SaveChangesAsync();
        using var guest=f.Client();var r=await guest.GetAsync($"/api/clinic/team/{id}/photo");Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Equal(bytes,await r.Content.ReadAsByteArrayAsync());
        Assert.NotNull((await guest.GetFromJsonAsync<PublicTeamResponse[]>("/api/clinic/team"))!.Single(x=>x.Id==id).PhotoVersion);
        await c.DeleteAsync($"{Root}/{id}");Assert.Equal(HttpStatusCode.NotFound,(await guest.GetAsync($"/api/clinic/team/{id}/photo")).StatusCode);
        Assert.True(await db.UserProfilePhotos.AnyAsync(x=>x.UserId==d.UserId));
    }
    [Fact] public async Task AnonymousCannotConfigure() {using var c=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync(Root)).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await c.PostAsJsonAsync(Root,new FeaturedDoctorRequest(Guid.NewGuid(),0))).StatusCode);}
}
