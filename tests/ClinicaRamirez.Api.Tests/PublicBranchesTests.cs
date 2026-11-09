using System.Net.Http.Json;
using ClinicaRamirez.Api.Controllers;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClinicaRamirez.Api.Tests;
public sealed class PublicBranchesTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    [Fact]
    public async Task PublicListReturnsActiveBranchesWithPersistedCoordinates()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var active = new Center { ClinicId = Clinic.SingletonId, Name = "Public branch", Code = "B" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Address = "Street", Latitude = -18.42m, Longitude = -66.58m };
        var hidden = new Center { ClinicId = Clinic.SingletonId, Name = "Hidden", Code = "B" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Address = "Street", IsActive = false };
        db.Centers.AddRange(active, hidden); await db.SaveChangesAsync();
        using var client = f.Client();
        var rows = (await client.GetFromJsonAsync<PublicBranchResponse[]>("/api/clinic/branches"))!;
        Assert.DoesNotContain(rows, x => x.Id == hidden.Id);
        Assert.Equal(-18.42m, rows.Single(x => x.Id == active.Id).Latitude);
        Assert.Equal(-66.58m, rows.Single(x => x.Id == active.Id).Longitude);
    }
}
