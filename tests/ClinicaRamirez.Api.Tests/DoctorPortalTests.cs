using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.DTOs.Doctors;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;
public sealed class DoctorPortalTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private async Task<(HttpClient Client, DoctorProfile Doctor)> Doctor(bool profile = true, bool active = true)
    {
        var email = await f.CreateUser();
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email); var role = await db.Roles.SingleAsync(x => x.Code == "DOCTOR");
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        var doctor = new DoctorProfile { UserId = user.Id, ProfessionalLicense = Guid.NewGuid().ToString("N").ToUpperInvariant(), IsActive = active };
        if (profile) db.DoctorProfiles.Add(doctor);
        await db.SaveChangesAsync(); var client = f.Client(); var login = await f.Login(client, email); client.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        return (client, doctor);
    }
    private async Task<Appointment> Appointment(Guid doctor, DateOnly date, int hour = 9)
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var center = new Center { ClinicId = Clinic.SingletonId, Name = "Central portal", Code = "PORTAL_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Address = "Test" };
        var patient = new PatientProfile { User = new User { FirstName = "Andrea", LastName = "Bejar", DocumentType="CI", DocumentCountryCode="BO", DocumentNumber= Guid.NewGuid().ToString("N").ToUpperInvariant(), BirthDate = new(1990,1,1), Phone = "70000000", CanLogin = false }, BloodType = "UNKNOWN", EmergencyContactName = "Test", EmergencyContactPhone = "70000000" };
        db.Centers.Add(center); db.PatientProfiles.Add(patient);
        var role = await db.Roles.SingleAsync(x => x.Code == "PATIENT"); db.UserRoles.Add(new UserRole { User = patient.User, RoleId = role.Id });
        var a = new Appointment { PatientId = patient.Id, DoctorId = doctor, CenterId = center.Id, SpecialtyId = await db.Specialties.Select(x => x.Id).FirstAsync(), AppointmentDate = date, StartTime = new(hour,0), EndTime = new(hour,30), Reason = "Consulta", Notes = "Private administrative note" };
        db.Appointments.Add(a); await db.SaveChangesAsync(); return a;
    }
    [Fact] public async Task OwnAppointmentsOnlyEvenWithForgedIds()
    {
        var (c,d) = await Doctor(); var (_,other) = await Doctor(); var own = await Appointment(d.Id,new(2026,10,13)); var hidden = await Appointment(other.Id,new(2026,10,13));
        foreach (var suffix in new[]{"",$"&doctorId={other.Id}&userId={other.UserId}"}) {
            var result = (await c.GetFromJsonAsync<DoctorAgenda>("/api/doctor/me/appointments?date=2026-10-13"+suffix))!;
            Assert.Equal(own.Id, Assert.Single(result.Items).AppointmentId); Assert.DoesNotContain(result.Items,x=>x.AppointmentId==hidden.Id);
            Assert.Equal("Andrea",result.Items[0].Patient.FirstName); Assert.Equal("Bejar",result.Items[0].Patient.LastName);
        }
    }
    [Theory][InlineData("day",1)][InlineData("week",3)][InlineData("month",5)]
    public async Task DateRangesAndOrdering(string view,int expected)
    {
        var(c,d)=await Doctor();
        foreach(var day in new[]{1,12,13,18,31}) await Appointment(d.Id,new(2026,10,day));
        await Appointment(d.Id,new(2026,11,1)); await Appointment(d.Id,new(2026,9,30));
        var result=(await c.GetFromJsonAsync<DoctorAgenda>($"/api/doctor/me/appointments?date=2026-10-13&view={view}"))!;
        Assert.Equal(expected,result.Items.Length); Assert.Equal(result.Items.OrderBy(x=>x.Date).ThenBy(x=>x.StartTime),result.Items);
    }
    [Fact] public async Task ChronologicalTimesAndMinimalDto()
    {
        var(c,d)=await Doctor();await Appointment(d.Id,new(2026,10,13),15);await Appointment(d.Id,new(2026,10,13),8);
        var response=await c.GetAsync("/api/doctor/me/appointments?date=2026-10-13");var result=(await response.Content.ReadFromJsonAsync<DoctorAgenda>())!;
        Assert.Equal(8,result.Items[0].StartTime.Hour);var text=await response.Content.ReadAsStringAsync();
        foreach(var field in new[]{"password","token","notes","doctorId","userId","email","documentNumber"}) Assert.DoesNotContain("\""+field+"\"",text,StringComparison.OrdinalIgnoreCase);
    }
    [Theory][InlineData(false,true)][InlineData(true,false)] public async Task MissingOrInactiveProfileControlled(bool profile,bool active)
    {var(c,_)=await Doctor(profile,active);Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/doctor/me/appointments?date=2026-10-13")).StatusCode);}
    [Fact] public async Task AuthenticationAndRoleRequired()
    {using var c=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/doctor/me/appointments?date=2026-10-13")).StatusCode);var login=await f.Login(c);c.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/doctor/me/appointments?date=2026-10-13")).StatusCode);}
    [Theory][InlineData("date=2026-10-13&view=year")][InlineData("date=not-date")][InlineData("")][InlineData("date=9999-12-31")]
    public async Task InvalidRangeControlled(string query){var(c,_)=await Doctor();Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/api/doctor/me/appointments?"+query)).StatusCode);}
    [Fact] public async Task RoleRevokedTakesEffectImmediately(){var(c,d)=await Doctor();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();await db.UserRoles.Where(x=>x.UserId==d.UserId).ExecuteDeleteAsync();Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/doctor/me/appointments?date=2026-10-13")).StatusCode);}
    [Fact] public async Task EmptyDayReturnsEmpty(){var(c,_)=await Doctor();Assert.Empty((await c.GetFromJsonAsync<DoctorAgenda>("/api/doctor/me/appointments?date=2026-10-13"))!.Items);}
}
