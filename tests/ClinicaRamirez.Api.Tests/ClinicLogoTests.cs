using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using Xunit;

namespace ClinicaRamirez.Api.Tests;

public sealed class ClinicLogoTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private const string UploadPath = "/api/admin/clinic/logo", GetPath = "/api/clinic/logo";
    private async Task<HttpClient> Admin()
    {
        var client = f.Client(); var login = await f.Login(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken); return client;
    }
    private static byte[] Image(SKEncodedImageFormat format, int width = 30, int height = 10)
    {
        using var bitmap = new SKBitmap(width, height); bitmap.Erase(new SKColor(20, 80, 160, 100));
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(format, 100);
        return data.ToArray();
    }
    private static async Task<HttpResponseMessage> Upload(HttpClient client, byte[] bytes, string type = "image/png", string name = "logo.png")
    {
        using var form = new MultipartFormDataContent(); var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type); form.Add(content, "file", name);
        return await client.PutAsync(UploadPath, form);
    }
    [Theory]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public async Task GuardaYDevuelveBytesOriginales(SKEncodedImageFormat format, string type)
    {
        using var admin = await Admin(); var bytes = Image(format);
        var response = await Upload(admin, bytes, type, "logo.test"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var metadata = (await response.Content.ReadFromJsonAsync<ClinicLogoResponse>())!;
        Assert.Equal(bytes.Length, metadata.Size); Assert.Equal(type, metadata.ContentType); Assert.Equal(DateTimeKind.Utc, metadata.UpdatedAt.Kind);
        using var anonymous = f.Client(); var get = await anonymous.GetAsync(GetPath);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode); Assert.Equal(type, get.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await get.Content.ReadAsByteArrayAsync()); Assert.Equal("nosniff", get.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(get.Headers.CacheControl!.MustRevalidate); Assert.NotNull(get.Headers.ETag);
        using var request = new HttpRequestMessage(HttpMethod.Get, GetPath); request.Headers.IfNoneMatch.Add(get.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await anonymous.SendAsync(request)).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clinic = await db.Clinics.AsNoTracking().SingleAsync(); Assert.Equal(bytes, clinic.LogoData);
    }
    [Fact] public async Task RechazaMayorDe5Mb()
    {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Upload(c, new byte[5 * 1024 * 1024 + 1])).StatusCode);
    }
    [Theory][InlineData("image/svg+xml")][InlineData("application/octet-stream")][InlineData("text/plain")]
    public async Task RechazaTipoNoPermitido(string type)
    {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, Image(SKEncodedImageFormat.Png), type)).StatusCode);
    }
    [Fact] public async Task RechazaEjecutableConNombrePng()
    {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, "MZfake executable"u8.ToArray())).StatusCode);
    }
    [Fact] public async Task RechazaSvgDisfrazado()
    {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"u8.ToArray())).StatusCode);
    }
    [Fact] public async Task RechazaVacio()
    {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, [])).StatusCode);
    }
    [Fact] public async Task RechazaContentTypeFalso()
    {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, Image(SKEncodedImageFormat.Jpeg))).StatusCode);
    }
    [Theory][InlineData(SKEncodedImageFormat.Png, "image/png")][InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")][InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public async Task RechazaImagenTruncada(SKEncodedImageFormat format, string type)
    {
        using var c = await Admin(); var bytes = Image(format);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, bytes[..(bytes.Length / 2)], type)).StatusCode);
    }
    [Fact] public async Task RechazaDimensionesExcesivas()
    {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, Image(SKEncodedImageFormat.Png, 8200, 1))).StatusCode);
    }
    [Fact] public async Task ReemplazaSinModificarCalidadYFalloConservaLogo()
    {
        using var c = await Admin(); var first = Image(SKEncodedImageFormat.Png); var next = Image(SKEncodedImageFormat.Webp);
        Assert.Equal(HttpStatusCode.OK, (await Upload(c, first)).StatusCode);
        var firstGet = await c.GetAsync(GetPath);
        Assert.Equal(HttpStatusCode.OK, (await Upload(c, next, "image/webp", "reemplazo.webp")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(c, [1, 2, 3])).StatusCode);
        var get = await c.GetAsync(GetPath); Assert.Equal(next, await get.Content.ReadAsByteArrayAsync()); Assert.NotEqual(firstGet.Headers.ETag, get.Headers.ETag);
    }
    [Fact] public async Task EliminarBorraTodosLosCamposYGet404()
    {
        using var c = await Admin(); await Upload(c, Image(SKEncodedImageFormat.Png));
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync(UploadPath)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync(GetPath)).StatusCode);
        using var scope = f.Services.CreateScope(); var clinic = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clinics.SingleAsync();
        Assert.Null(clinic.LogoData); Assert.Null(clinic.LogoContentType); Assert.Null(clinic.LogoFileName); Assert.Null(clinic.LogoSize); Assert.Null(clinic.LogoUpdatedAt);
    }
    [Fact] public async Task EscriturasSinPermiso403YSinSesion401()
    {
        using var c = f.Client(); var login = await f.Login(c, await f.CreateUser()); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(c, Image(SKEncodedImageFormat.Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync(UploadPath)).StatusCode);
        c.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await Upload(c, Image(SKEncodedImageFormat.Png))).StatusCode);
    }
    [Fact] public async Task AuditoriaSoloMetadataPermitidaYJsonSinBytes()
    {
        using var c = await Admin(); await Upload(c, Image(SKEncodedImageFormat.Png));
        var response = await c.GetAsync("/api/admin/clinic");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.TryGetProperty("logoData", out _));
        var logo = json.RootElement.GetProperty("logo"); Assert.Equal(4, logo.EnumerateObject().Count());
        await c.DeleteAsync(UploadPath);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.AuditLogs.Where(x => x.Action == "clinic_logo_uploaded" || x.Action == "clinic_logo_removed").ToArrayAsync();
        Assert.Contains(logs, x => x.Action == "clinic_logo_uploaded"); Assert.Contains(logs, x => x.Action == "clinic_logo_removed");
        foreach (var log in logs.Where(x => x.MetadataJson != null))
        {
            using var metadata = JsonDocument.Parse(log.MetadataJson!);
            Assert.All(metadata.RootElement.EnumerateObject(), property => Assert.Contains(property.Name, new[] { "contentType", "size" }));
        }
    }
}
