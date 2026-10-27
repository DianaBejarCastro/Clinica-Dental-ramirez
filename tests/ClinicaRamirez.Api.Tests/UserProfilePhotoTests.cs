using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;
namespace ClinicaRamirez.Api.Tests;

public sealed class UserProfilePhotoTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private async Task<HttpClient> Admin() {
        var c = f.Client(); var login = await f.Login(c); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken); return c;
    }
    private async Task<Guid> Person(string role, bool mixed = false) {
        var email = await f.CreateUser();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        db.UserRoles.Add(new() { UserId = user.Id, RoleId = await db.Roles.Where(x => x.Code == role).Select(x => x.Id).SingleAsync() });
        if (mixed) db.UserRoles.Add(new() { UserId = user.Id, RoleId = await db.Roles.Where(x => x.Code == "PATIENT").Select(x => x.Id).SingleAsync() });
        await db.SaveChangesAsync(); return user.Id;
    }
    private static string Path(Guid id) => $"/api/admin/users/{id}/profile-photo";
    private static byte[] Image(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int w = 1080, int h = 1350) {
        using var bitmap = new SKBitmap(w, h); bitmap.Erase(SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(format, 90); return data.ToArray();
    }
    private static Task<HttpResponseMessage> Upload(HttpClient c, Guid id, byte[] bytes, string mime = "image/png") {
        var form = new MultipartFormDataContent(); var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(file, "file", "../../renamed.png"); return c.PutAsync(Path(id), form);
    }
    [Theory][InlineData("ADMINISTRATOR")][InlineData("SECRETARY")][InlineData("DOCTOR")][InlineData("CASHIER")][InlineData("DENTAL_ASSISTANT")]
    public async Task OptionalStaffPhotoLifecycle(string role) {
        using var c = await Admin(); var id = await Person(role);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(Path(id) + "/content")).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.UserProfilePhotos.AnyAsync(x => x.UserId == id));
        Assert.Equal(HttpStatusCode.OK, (await Upload(c, id, Image())).StatusCode);
        var first = await db.UserProfilePhotos.AsNoTracking().SingleAsync(x => x.UserId == id);
        Assert.Equal(1080, first.Width); Assert.Equal(1350, first.Height);
        Assert.Equal(HttpStatusCode.OK, (await Upload(c, id, Image())).StatusCode);
        var second = await db.UserProfilePhotos.AsNoTracking().SingleAsync(x => x.UserId == id);
        Assert.Equal(first.Id, second.Id); Assert.Equal(first.CreatedAt, second.CreatedAt);
        var content = await c.GetAsync(Path(id) + "/content"); Assert.Equal("image/webp", content.Content.Headers.ContentType!.MediaType);
        using var decoded = SKBitmap.Decode(await content.Content.ReadAsByteArrayAsync()); Assert.Equal(1080, decoded.Width); Assert.Equal(1350, decoded.Height);
        Assert.DoesNotContain("imageData", await (await c.GetAsync(Path(id))).Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync(Path(id))).StatusCode);
        Assert.False(await db.UserProfilePhotos.AnyAsync(x => x.UserId == id));
        Assert.True(await db.Users.AnyAsync(x => x.Id == id));
        var logs = await db.AuditLogs.Where(x => x.EntityId == id && x.Action.StartsWith("user_profile_photo")).ToArrayAsync();
        Assert.Equal(3, logs.Length);
        Assert.All(logs, log => { Assert.True(log.MetadataJson!.Length < 100); Assert.DoesNotContain("ImageData", log.MetadataJson); });
    }
    [Theory][InlineData(SKEncodedImageFormat.Png,"image/png")][InlineData(SKEncodedImageFormat.Jpeg,"image/jpeg")][InlineData(SKEncodedImageFormat.Webp,"image/webp")]
    public async Task ActualFormatsAccepted(SKEncodedImageFormat format, string mime) {
        using var c=await Admin(); Assert.Equal(HttpStatusCode.OK,(await Upload(c,await Person("DOCTOR"),Image(format),mime)).StatusCode);
    }
    [Theory][InlineData(1080,1080)][InlineData(1920,1080)][InlineData(800,1000)]
    public async Task IncorrectDimensionsRejected(int w,int h) {
        using var c=await Admin(); Assert.Equal(HttpStatusCode.BadRequest,(await Upload(c,await Person("DOCTOR"),Image(w:w,h:h))).StatusCode);
    }
    [Theory][InlineData("image/png")][InlineData("image/svg+xml")][InlineData("application/octet-stream")]
    public async Task InvalidContentRejected(string mime) {
        using var c=await Admin(); Assert.Equal(HttpStatusCode.BadRequest,(await Upload(c,await Person("DOCTOR"),"MZ fake"u8.ToArray(),mime)).StatusCode);
    }
    [Fact] public async Task MimeMismatchAndOversizeRejected() {
        using var c=await Admin(); var id=await Person("DOCTOR");
        Assert.Equal(HttpStatusCode.BadRequest,(await Upload(c,id,Image(SKEncodedImageFormat.Jpeg))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Upload(c,id,new byte[10*1024*1024+1])).StatusCode);
        var bytes=Image(); Assert.Equal(HttpStatusCode.BadRequest,(await Upload(c,id,bytes[..^16])).StatusCode);
    }
    [Fact] public async Task PatientExcludedButMixedStaffAllowed() {
        using var c=await Admin();
        Assert.Equal(HttpStatusCode.BadRequest,(await Upload(c,await Person("PATIENT"),Image())).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Upload(c,await Person("DOCTOR",true),Image())).StatusCode);
    }
    [Fact] public async Task AnonymousAndPatientCannotManageOrRead() {
        var id=await Person("DOCTOR");
        using var anon=f.Client(); Assert.Equal(HttpStatusCode.Unauthorized,(await anon.GetAsync(Path(id))).StatusCode);
        var patient=await Person("PATIENT"); using var scope=f.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var c=f.Client(); var login=await f.Login(c,await db.Users.Where(x=>x.Id==patient).Select(x=>x.Email!).SingleAsync());
        c.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,(await Upload(c,id,Image())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.DeleteAsync(Path(id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync(Path(id)+"/content")).StatusCode);
    }
    [Fact] public async Task UniqueAndRestrictAreRealDatabaseConstraints() {
        var id=await Person("DOCTOR"); using var c=await Admin(); await Upload(c,id,Image());
        using var scope=f.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Users\" WHERE \"Id\"={id}"));
        var row=await db.UserProfilePhotos.AsNoTracking().SingleAsync(x=>x.UserId==id);
        row.Id=Guid.NewGuid(); db.UserProfilePhotos.Add(row);
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
        var entity=db.Model.FindEntityType(typeof(UserProfilePhoto))!;
        Assert.Single(entity.GetForeignKeys()); Assert.All(entity.GetForeignKeys(),fk=>Assert.Equal(DeleteBehavior.Restrict,fk.DeleteBehavior));
    }
}
