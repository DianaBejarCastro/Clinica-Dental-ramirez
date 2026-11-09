using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;
public sealed class ClinicServicesTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private const string Root = "/api/admin/clinic/services";
    private async Task<HttpClient> Admin() { var c = f.Client(); var login = await f.Login(c); c.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken); return c; }
    private static ClinicServiceRequest Request(string name = "Servicio", int order = 0, bool active = true, bool published = true) => new(name, "Descripción editorial", "TEAL", order, active, published, null);
    private static async Task<ClinicServiceResponse> Create(HttpClient c, ClinicServiceRequest request) {
        var r = await c.PostAsJsonAsync(Root, request); Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<ClinicServiceResponse>())!;
    }
    [Fact] public async Task CreateUpdateDeactivateAndAudit() {
        using var c = await Admin(); var item = await Create(c, Request());
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"{Root}/{item.Id}", Request("Editado") with { VisualVariant = "MINT" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PatchAsJsonAsync($"{Root}/{item.Id}/status", new { isActive = false })).StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.ClinicServices.SingleAsync(x => x.Id == item.Id);
        Assert.Equal(Clinic.SingletonId, row.ClinicId); Assert.Equal("Editado", row.Name); Assert.Equal("MINT", row.VisualVariant); Assert.False(row.IsActive);
        Assert.Equal(3, await db.AuditLogs.CountAsync(x => x.EntityId == item.Id));
    }
    [Fact] public async Task PublicFiltersAndStableOrder() {
        using var c = await Admin(); var first = await Create(c, Request("Primero", 10)); var last = await Create(c, Request("Último", 20));
        var hidden = await Create(c, Request("Privado", 0, published: false)); var inactive = await Create(c, Request("Inactivo", 0, active: false));
        using var guest = f.Client(); var rows = (await guest.GetFromJsonAsync<PublicClinicServiceResponse[]>("/api/clinic/services"))!;
        Assert.DoesNotContain(rows, x => x.Id == hidden.Id || x.Id == inactive.Id);
        Assert.True(Array.FindIndex(rows, x => x.Id == first.Id) < Array.FindIndex(rows, x => x.Id == last.Id));
    }
    [Theory][InlineData("", "TEAL", 0)][InlineData("   ", "TEAL", 0)][InlineData("Test", "background:red", 0)][InlineData("Test", "TEAL", -1)]
    public async Task InvalidInputRejected(string name, string color, int order) {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Root, Request(name, order) with { VisualVariant = color })).StatusCode);
    }
    [Fact] public async Task InvalidSpecialtyRejected() {
        using var c = await Admin(); Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Root, Request() with { SpecialtyId = Guid.NewGuid() })).StatusCode);
    }
    [Fact] public async Task OptionalSpecialtyLinkedAndRestrictsDeletion() {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var specialty = new Specialty { Code = "TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Name = "Test", IsActive = false }; db.Specialties.Add(specialty); await db.SaveChangesAsync();
        using var c = await Admin(); var item = await Create(c, Request() with { SpecialtyId = specialty.Id }); Assert.Equal(specialty.Id, item.SpecialtyId);
        Assert.Equal(HttpStatusCode.Conflict, (await c.GetAsync($"/api/admin/deletion/specialties/{specialty.Id}/check")).StatusCode);
        var fks = db.Model.FindEntityType(typeof(ClinicService))!.GetForeignKeys().ToArray();
        Assert.Equal(2, fks.Length); Assert.All(fks, fk => Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior));
    }
    [Fact] public async Task OrphanClinicRejectedByDatabase() {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.ClinicServices.Add(new ClinicService { ClinicId = Guid.NewGuid(), Name = "Orphan" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact] public async Task DeleteRequiresInactiveAndPasswordThenAudits() {
        using var c = await Admin(); var item = await Create(c, Request());
        var path = $"/api/admin/deletion/clinic-services/{item.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await c.GetAsync(path + "/check")).StatusCode);
        await c.PatchAsJsonAsync($"{Root}/{item.Id}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(path + "/check")).StatusCode);
        var response = await c.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path) { Content = JsonContent.Create(new { password = f.Password }) });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ClinicServices.AnyAsync(x => x.Id == item.Id));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == item.Id && x.Action == "clinic_service_deleted"));
    }
    [Fact] public async Task AnonymousCannotManage() {
        using var c = f.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(Root)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync(Root, Request())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/clinic/services")).StatusCode);
    }
}
