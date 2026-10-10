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
public sealed class ClinicSocialNetworksTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private const string Root = "/api/admin/clinic/social-networks", Public = "/api/clinic/social-networks";
    private async Task<HttpClient> Admin() { var c = f.Client(); var login = await f.Login(c); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken); return c; }
    private static ClinicSocialNetworkCreateRequest Data(string name = " Instagram ", int order = 0, bool active = true) => new(name, $"https://example.com/{Guid.NewGuid()}", order, active);
    private static async Task<ClinicSocialNetworkResponse> Create(HttpClient c, ClinicSocialNetworkCreateRequest? data = null)
    { var response = await c.PostAsJsonAsync(Root, data ?? Data()); Assert.Equal(HttpStatusCode.Created, response.StatusCode); return (await response.Content.ReadFromJsonAsync<ClinicSocialNetworkResponse>())!; }
    private static byte[] Image(SKEncodedImageFormat format) { using var bitmap = new SKBitmap(10, 10); bitmap.Erase(new SKColor(20, 80, 160, 100)); using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(format, 100); return data.ToArray(); }
    private static async Task<HttpResponseMessage> Upload(HttpClient c, Guid id, byte[] bytes, string type = "image/png")
    { using var form = new MultipartFormDataContent(); var content = new ByteArrayContent(bytes); content.Headers.ContentType = new MediaTypeHeaderValue(type); form.Add(content, "file", "logo.png"); return await c.PutAsync($"{Root}/{id}/logo", form); }
    [Fact] public async Task CreaVariasRedesRelacionadasSinBytesEnJson()
    {
        using var c = await Admin(); var a = await Create(c); var b = await Create(c);
        Assert.NotEqual(a.Id, b.Id); Assert.Equal("Instagram", a.Name); Assert.True(a.IsActive); Assert.Equal(DateTimeKind.Utc, a.CreatedAt.Kind);
        var json = await c.GetStringAsync($"{Root}/{a.Id}"); Assert.DoesNotContain("logoData", json, StringComparison.OrdinalIgnoreCase);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(await db.ClinicSocialNetworks.Where(x => x.Id == a.Id).Select(x => x.ClinicId).SingleAsync(), await db.ClinicSocialNetworks.Where(x => x.Id == b.Id).Select(x => x.ClinicId).SingleAsync());
    }
    [Theory][InlineData("")][InlineData("not-url")][InlineData("javascript:alert(1)")][InlineData("ftp://example.com")][InlineData("https://")]
    public async Task RechazaUrlInvalida(string url)
    { using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Root, Data() with { Url = url })).StatusCode); }
    [Theory][InlineData("", 0)][InlineData("   ", 0)][InlineData("Instagram", -1)]
    public async Task RechazaNombreUOrden(string name, int order)
    { using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Root, Data(name, order))).StatusCode); }
    [Fact] public async Task DuplicadosDevuelven409InclusoInactiva()
    { using var c = await Admin(); var data = Data(active: false); await Create(c, data); Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Root, data)).StatusCode); }
    [Fact] public async Task EditaDesactivaReactivaYNoPermiteDelete()
    {
        using var c = await Admin(); var item = await Create(c);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"{Root}/{item.Id}", new ClinicSocialNetworkUpdateRequest("Facebook", item.Url, 3))).StatusCode);
        foreach (var active in new[] { false, true }) { var response = await c.PatchAsJsonAsync($"{Root}/{item.Id}/status", new { isActive = active }); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(active, (await response.Content.ReadFromJsonAsync<ClinicSocialNetworkResponse>())!.IsActive); }
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await c.DeleteAsync($"{Root}/{item.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync($"{Root}/{item.Id}")).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ClinicSocialNetworks.Remove(await db.ClinicSocialNetworks.SingleAsync(x => x.Id == item.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact] public async Task PublicoSoloActivasOrdenadasSinDatosInternos()
    {
        using var c = await Admin(); await Create(c, Data("Zeta", 99)); await Create(c, Data("Alfa", 99)); var inactive = await Create(c, Data(active: false));
        using var anonymous = f.Client(); var response = await anonymous.GetAsync(Public); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<PublicClinicSocialNetworkResponse[]>())!; Assert.DoesNotContain(items, x => x.Id == inactive.Id);
        var admin = (await c.GetFromJsonAsync<ClinicSocialNetworkResponse[]>(Root))!;
        Assert.Equal(admin.Where(x => x.IsActive).Select(x => x.Id), items.Select(x => x.Id));
        Assert.True(Array.FindIndex(items, x => x.Name == "Alfa") < Array.FindIndex(items, x => x.Name == "Zeta"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); Assert.All(json.RootElement.EnumerateArray(), x => Assert.Equal(new[] { "id", "name", "url", "hasLogo" }, x.EnumerateObject().Select(p => p.Name)));
    }
    [Theory][InlineData(SKEncodedImageFormat.Png, "image/png")][InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")][InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public async Task LogoConservaOriginalesYTipo(SKEncodedImageFormat format, string type)
    {
        using var c = await Admin(); var item = await Create(c); var bytes = Image(format);
        Assert.Equal(HttpStatusCode.OK, (await Upload(c, item.Id, bytes, type)).StatusCode);
        using var anonymous = f.Client(); var response = await anonymous.GetAsync($"{Public}/{item.Id}/logo");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(type, response.Content.Headers.ContentType!.MediaType); Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
    }
    [Theory][InlineData(0, "image/png")][InlineData(10, "image/png")][InlineData(2097153, "image/png")][InlineData(10, "image/svg+xml")]
    public async Task RechazaVacioFalsoGrandeYSvg(int size, string type)
    { using var c = await Admin(); var item = await Create(c); Assert.Equal(size > 2097152 ? HttpStatusCode.RequestEntityTooLarge : HttpStatusCode.BadRequest, (await Upload(c, item.Id, new byte[size], type)).StatusCode); }
    [Fact] public async Task ReemplazaEliminaLogoYRegistraAuditoria()
    {
        using var c = await Admin(); var item = await Create(c); await Upload(c, item.Id, Image(SKEncodedImageFormat.Png));
        var bytes = Image(SKEncodedImageFormat.Webp); await Upload(c, item.Id, bytes, "image/webp");
        Assert.Equal(bytes, await c.GetByteArrayAsync($"{Public}/{item.Id}/logo"));
        await c.PutAsJsonAsync($"{Root}/{item.Id}", new ClinicSocialNetworkUpdateRequest("Nueva", item.Url));
        await c.PatchAsJsonAsync($"{Root}/{item.Id}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.NoContent, (await c.DeleteAsync($"{Root}/{item.Id}/logo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Public}/{item.Id}/logo")).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.ClinicSocialNetworks.SingleAsync(x => x.Id == item.Id);
        Assert.Null(stored.LogoData); Assert.Null(stored.LogoContentType); Assert.Null(stored.LogoFileName); Assert.Null(stored.LogoSize);
        var logs = await db.AuditLogs.Where(x => x.EntityId == item.Id).ToArrayAsync();
        foreach (var action in new[] { "created", "updated", "status_changed", "logo_uploaded", "logo_removed" }) Assert.Contains(logs, x => x.Action == "clinic_social_network_" + action);
        foreach (var log in logs.Where(x => x.MetadataJson != null)) { using var metadata = JsonDocument.Parse(log.MetadataJson!); Assert.All(metadata.RootElement.EnumerateObject(), p => Assert.Contains(p.Name, new[] { "contentType", "size" })); }
    }
    [Fact] public async Task PermisosYRecursosInexistentes()
    {
        using var admin = await Admin(); var item = await Create(admin);
        using var c = f.Client(); var login = await f.Login(c, await f.CreateUser()); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(Root)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync(Root, Data())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"{Root}/{item.Id}", new ClinicSocialNetworkUpdateRequest("Nueva", item.Url))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PatchAsJsonAsync($"{Root}/{item.Id}/status", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(c, item.Id, Image(SKEncodedImageFormat.Png))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync($"{Root}/{item.Id}/logo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Root}/{Guid.NewGuid()}")).StatusCode);
    }
}
