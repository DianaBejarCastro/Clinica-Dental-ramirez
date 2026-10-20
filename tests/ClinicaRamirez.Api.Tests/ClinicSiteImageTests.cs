using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace ClinicaRamirez.Api.Tests;

public sealed class ClinicSiteImageTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private const string AdminPath = "/api/admin/clinic/site-images/hero", PublicPath = "/api/clinic/site-images/hero";
    private async Task<HttpClient> Admin() {
        var c = f.Client(); var login = await f.Login(c); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken); return c;
    }
    private static byte[] Image(SKEncodedImageFormat format = SKEncodedImageFormat.Png, int width = 1916, int height = 821) {
        using var bitmap = new SKBitmap(width, height); bitmap.Erase(new SKColor(40, 120, 110));
        using var image = SKImage.FromBitmap(bitmap); using var bytes = image.Encode(format, 95); return bytes.ToArray();
    }
    private static Task<HttpResponseMessage> Upload(HttpClient c, byte[] bytes, string mime = "image/png", string focal = "0.25", string alt = "Instalaciones") {
        var form = new MultipartFormDataContent(); var file = new ByteArrayContent(bytes); file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(file, "file", "photo.png"); form.Add(new StringContent(focal), "focalPointX"); form.Add(new StringContent("0.5"), "focalPointY"); form.Add(new StringContent(alt), "altText");
        return c.PutAsync(AdminPath, form);
    }
    [Theory][InlineData(SKEncodedImageFormat.Png, "image/png")][InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")][InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public async Task ValidImageStoredAndPublicBytes(SKEncodedImageFormat format, string mime) {
        using var c = await Admin(); var bytes = Image(format);
        var response = await Upload(c, bytes, mime); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metadata = (await response.Content.ReadFromJsonAsync<HeroImageResponse>())!;
        Assert.Equal(1916, metadata.Width); Assert.Equal(821, metadata.Height); Assert.Equal(bytes.Length, metadata.Size);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ClinicSiteImages.SingleAsync(); Assert.Equal(Clinic.SingletonId, row.ClinicId); Assert.Equal(ClinicSiteImageType.HERO, row.ImageType); Assert.Equal(bytes, row.ImageData);
        Assert.Equal(.25m, row.FocalPointX); Assert.Equal(DateTimeKind.Utc, row.UpdatedAt.Kind);
        using var anonymous = f.Client(); var get = await anonymous.GetAsync(PublicPath);
        Assert.Equal(bytes, await get.Content.ReadAsByteArrayAsync()); Assert.Equal(mime, get.Content.Headers.ContentType!.MediaType);
        Assert.True(get.Headers.CacheControl!.MustRevalidate); Assert.NotNull(get.Headers.ETag);
        using var request = new HttpRequestMessage(HttpMethod.Get, PublicPath); request.Headers.IfNoneMatch.Add(get.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await anonymous.SendAsync(request)).StatusCode);
        var meta = await anonymous.GetFromJsonAsync<HeroPublicMetadata>(PublicPath + "/meta"); Assert.Equal("Instalaciones", meta!.AltText); Assert.Equal(.25m, meta.FocalPointX);
        Assert.DoesNotContain("imageData", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
    [Theory][InlineData(1920, 1080)][InlineData(1916, 820)][InlineData(821, 1916)]
    public async Task WrongDimensionsRejected(int w, int h) {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, Image(width: w, height: h))).StatusCode);
    }
    [Fact] public async Task OversizedRejected() { using var c = await Admin(); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Upload(c, new byte[10 * 1024 * 1024 + 1])).StatusCode); }
    [Theory][InlineData("image/png")][InlineData("image/svg+xml")][InlineData("application/octet-stream")]
    public async Task FakeOrDisallowedContentRejected(string mime) {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, "MZ fake image"u8.ToArray(), mime)).StatusCode);
    }
    [Fact] public async Task MimeMismatchAndTruncatedRejected() {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, Image(SKEncodedImageFormat.Jpeg))).StatusCode);
        var bytes = Image(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, bytes[..^12])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, [])).StatusCode);
    }
    [Theory][InlineData("-0.1")][InlineData("1.1")]
    public async Task InvalidFocalPointRejected(string focal) { using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, Image(), focal: focal)).StatusCode); }
    [Fact] public async Task ReplaceKeepsOneRowAndAudits() {
        using var c = await Admin(); await c.DeleteAsync(AdminPath); await Upload(c, Image());
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await db.ClinicSiteImages.AsNoTracking().SingleAsync(); var bytes = Image(SKEncodedImageFormat.Webp);
        Assert.Equal(HttpStatusCode.OK, (await Upload(c, bytes, "image/webp")).StatusCode);
        var next = await db.ClinicSiteImages.AsNoTracking().SingleAsync(); Assert.Equal(first.Id, next.Id); Assert.Equal(first.CreatedAt, next.CreatedAt); Assert.Equal(bytes, next.ImageData);
        await c.DeleteAsync(AdminPath);
        var logs = await db.AuditLogs.Where(x => x.EntityId == first.Id).ToArrayAsync();
        Assert.Contains(logs, x => x.Action == "clinic_hero_uploaded"); Assert.Contains(logs, x => x.Action == "clinic_hero_updated"); Assert.Contains(logs, x => x.Action == "clinic_hero_deleted");
        foreach (var log in logs) { using var json = JsonDocument.Parse(log.MetadataJson!); Assert.All(json.RootElement.EnumerateObject(), p => Assert.Contains(p.Name, new[] { "assetId", "contentType", "width", "height", "size" })); }
    }
    [Fact] public async Task DeletePreservesClinicAndLogoAndEmptyIs404() {
        using var c = await Admin(); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = JsonSerializer.Serialize(await db.Clinics.AsNoTracking().SingleAsync());
        await Upload(c, Image()); Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync(AdminPath)).StatusCode);
        Assert.Empty(await db.ClinicSiteImages.ToListAsync()); Assert.Equal(before, JsonSerializer.Serialize(await db.Clinics.AsNoTracking().SingleAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(PublicPath)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(PublicPath + "/meta")).StatusCode);
    }
    [Fact] public async Task PermissionAndAuthenticationEnforced() {
        using var c = f.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await Upload(c, Image())).StatusCode);
        var login = await f.Login(c, await f.CreateUser()); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(c, Image())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync(AdminPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(AdminPath)).StatusCode);
    }
    [Fact] public async Task InactiveIsNotPublicAndFallbackUsesClinicName() {
        using var c = await Admin(); await Upload(c, Image(), alt: ""); using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var name = await db.Clinics.Select(x => x.Name).SingleAsync(); var meta = await c.GetFromJsonAsync<HeroPublicMetadata>(PublicPath + "/meta"); Assert.Equal("Portada de " + name, meta!.AltText);
        var row = await db.ClinicSiteImages.SingleAsync(); row.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(PublicPath)).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(PublicPath + "/meta")).StatusCode);
    }
    [Fact] public void RelationalModelIsRestrictedAndUniqueWithNoClinicBlobs() {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var model = db.Model.FindEntityType(typeof(ClinicSiteImage))!;
        Assert.Equal(DeleteBehavior.Restrict, Assert.Single(model.GetForeignKeys()).DeleteBehavior);
        Assert.Contains(model.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ClinicId", "ImageType" }));
        Assert.Equal("bytea", model.FindProperty("ImageData")!.GetColumnType());
        Assert.DoesNotContain(db.Model.FindEntityType(typeof(Clinic))!.GetProperties(), p => p.Name.StartsWith("Hero"));
    }
}
