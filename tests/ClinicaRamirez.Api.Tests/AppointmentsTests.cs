using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.DTOs.Appointments;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;

public sealed class AppointmentsTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private const string Path = "/api/admin/appointments";
    private static readonly DateOnly Date = new(2030, 1, 7); // Monday
    private sealed record Data(Guid Patient, Guid Doctor, Guid Center, Guid Specialty);
    private async Task<HttpClient> Client(params string[] permissions)
    {
        var email = f.Email;
        if (permissions.Length != 0)
        {
            email = await f.CreateUser();
            await Db(async db => {
                var user = await db.Users.SingleAsync(x => x.Email == email);
                var role = new Role { Code = "TEST_" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Name = Guid.NewGuid().ToString() };
                db.Roles.Add(role);
                foreach (var id in await db.Permissions.Where(x => permissions.Contains(x.Code)).Select(x => x.Id).ToArrayAsync())
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = id });
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            });
        }
        var client = f.Client(); var login = await f.Login(client, email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        return client;
    }
    private async Task Db(Func<AppDbContext, Task> action)
    {
        using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db); await db.SaveChangesAsync();
    }
    private async Task<Data> Setup(bool withBreak = false, bool multiple = false)
    {
        var email = await f.CreateUser(); Data result = null!;
        await Db(async db => {
            var user = await db.Users.SingleAsync(x => x.Email == email);
            var role = await db.Roles.SingleAsync(x => x.Code == "DOCTOR");
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            var patient = new PatientProfile { User = new User { CanLogin = false, DocumentType="CI", DocumentCountryCode="BO", DocumentNumber= "TEST" + Guid.NewGuid().ToString("N").ToUpperInvariant(), BirthDate = new(1995, 5, 10), Phone = "70000000", FirstName = "Paciente", LastName = "Citas" }, EmergencyContactName = "Test contact", EmergencyContactPhone = "71111111", BloodType = "UNKNOWN",  };
            var center = new Center { ClinicId = Clinic.SingletonId, Code = "C" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Name = "Citas", Address = "Prueba" };
            var specialty = new Specialty { Code = "S" + Guid.NewGuid().ToString("N").ToUpperInvariant(), Name = Guid.NewGuid().ToString() };
            var doctor = new DoctorProfile { ProfessionalLicense = "TEST-" + Guid.NewGuid().ToString("N").ToUpperInvariant(), UserId = user.Id };
            db.AddRange(patient, center, specialty, doctor);
            db.DoctorCenters.Add(new DoctorCenter { DoctorId = doctor.Id, CenterId = center.Id });
            db.DoctorSpecialties.Add(new DoctorSpecialty { DoctorId = doctor.Id, SpecialtyId = specialty.Id });
            db.DoctorSchedules.Add(new DoctorSchedule { DoctorId = doctor.Id, CenterId = center.Id, DayOfWeek = 1,
                StartTime = new(8, 0), EndTime = new(12, 0), HasBreak = withBreak,
                BreakStart = withBreak ? new TimeOnly(10, 0) : null, BreakEnd = withBreak ? new TimeOnly(10, 30) : null });
            if (multiple) db.DoctorSchedules.Add(new DoctorSchedule { DoctorId = doctor.Id, CenterId = center.Id, DayOfWeek = 1,
                StartTime = new(14, 0), EndTime = new(16, 0) });
            result = new(patient.Id, doctor.Id, center.Id, specialty.Id);
        });
        return result;
    }
    private static AppointmentCreateRequest Body(Data d, int start = 8, int end = 9) => new()
    {
        PatientId = d.Patient, DoctorId = d.Doctor, CenterId = d.Center, SpecialtyId = d.Specialty,
        AppointmentDate = Date, StartTime = new(start, 0), EndTime = new(end, 0), Reason = "Motivo privado", Notes = "Nota privada"
    };
    private static async Task<AppointmentResponse> Create(HttpClient client, Data d, int start = 8, int end = 9)
    {
        var response = await client.PostAsJsonAsync(Path, Body(d, start, end));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AppointmentResponse>())!;
    }
    private static string AvailabilityUrl(Data d, int duration = 30, string date = "2030-01-07") =>
        $"{Path}/availability?doctorId={d.Doctor}&centerId={d.Center}&specialtyId={d.Specialty}&date={date}&durationMinutes={duration}";
    private static async Task<AvailabilityResponse> Availability(HttpClient client, Data d, int duration = 30) =>
        (await client.GetFromJsonAsync<AvailabilityResponse>(AvailabilityUrl(d, duration)))!;

    [Fact] public async Task CitaValidaListadoYDetalle()
    {
        using var c = await Client(); var d = await Setup(); var a = await Create(c, d);
        Assert.Equal("SCHEDULED", a.Status); Assert.NotNull(a.CreatedByUserId);
        Assert.Equal(DateTimeKind.Utc, a.CreatedAt.Kind); Assert.Equal(DateTimeKind.Utc, a.UpdatedAt.Kind);
        Assert.Equal(a, await c.GetFromJsonAsync<AppointmentResponse>($"{Path}/{a.Id}"));
        var list = (await c.GetFromJsonAsync<AppointmentListResponse>($"{Path}?pageSize=100"))!;
        Assert.Contains(list.Items, x => x.Id == a.Id);
    }

    [Fact] public async Task PresentationNamesRequireOnlyAppointmentView()
    {
        using var admin = await Client(); var data = await Setup(); var created = await Create(admin, data);
        using var viewer = await Client("appointments.view");
        var list = (await viewer.GetFromJsonAsync<AppointmentListResponse>($"{Path}?pageSize=100"))!;
        var appointment = Assert.Single(list.Items, x => x.Id == created.Id);
        Assert.Equal("Paciente Citas", appointment.PatientName);
        Assert.Equal("Prueba Integración", appointment.DoctorName);
        Assert.Equal("Citas", appointment.CenterName);
        Assert.Equal(appointment, await viewer.GetFromJsonAsync<AppointmentResponse>($"{Path}/{created.Id}"));
        foreach (var catalog in new[] { "patients", "doctors", "centers" })
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/admin/" + catalog)).StatusCode);
        var json = await viewer.GetStringAsync($"{Path}/{created.Id}");
        foreach (var field in new[] { "passwordHash", "accessToken", "refreshToken", "email", "documentNumber", "user" })
            Assert.DoesNotContain("\"" + field + "\"", json, StringComparison.OrdinalIgnoreCase);
    }
    [Theory]
    [InlineData("patient")][InlineData("doctor")][InlineData("user")][InlineData("center")][InlineData("specialty")][InlineData("assignment")]
    public async Task RechazaInactivos(string resource)
    {
        using var c = await Client(); var d = await Setup();
        await Db(async db => {
            switch (resource)
            {
                case "patient": (await db.PatientProfiles.FindAsync(d.Patient))!.IsActive = false; break;
                case "doctor": (await db.DoctorProfiles.FindAsync(d.Doctor))!.IsActive = false; break;
                case "user": (await db.DoctorProfiles.Include(x => x.User).SingleAsync(x => x.Id == d.Doctor)).User.IsActive = false; break;
                case "center": (await db.Centers.FindAsync(d.Center))!.IsActive = false; break;
                case "specialty": (await db.Specialties.FindAsync(d.Specialty))!.IsActive = false; break;
                case "assignment": (await db.DoctorCenters.FindAsync(d.Doctor, d.Center))!.IsActive = false; break;
            }
        });
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, Body(d))).StatusCode);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task RechazaSinAsignacion(bool center)
    {
        using var c = await Client(); var d = await Setup(); var other = await Setup();
        var invalid = center ? d with { Center = other.Center } : d with { Specialty = other.Specialty };
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, Body(invalid))).StatusCode);
    }
    [Theory][InlineData(7, 8)][InlineData(11, 13)][InlineData(10, 11)][InlineData(9, 11)][InlineData(9, 9)][InlineData(10, 9)]
    public async Task RechazaFueraDeHorarioDescansoOHorasInvalidas(int start, int end)
    {
        using var c = await Client(); var d = await Setup(true);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, Body(d, start, end))).StatusCode);
    }
    [Fact] public async Task RechazaSolapamientoYPermiteAdyacentes()
    {
        using var c = await Client(); var d = await Setup(); await Create(c, d);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Path, Body(d))).StatusCode);
        await Create(c, d, 9, 10);
    }
    [Fact] public async Task CreacionesConcurrentesNoDuplicanReserva()
    {
        using var c = await Client(); var d = await Setup();
        var responses = await Task.WhenAll(c.PostAsJsonAsync(Path, Body(d)), c.PostAsJsonAsync(Path, Body(d)));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }
    [Fact] public async Task DisponibilidadUnBloqueYDiferentesDuraciones()
    {
        using var c = await Client(); var d = await Setup();
        var slots = (await Availability(c, d)).Slots;
        Assert.Equal(8, slots.Length); Assert.Equal(new TimeOnly(8, 0), slots[0].StartTime);
        Assert.Equal(new TimeOnly(12, 0), slots[^1].EndTime);
        Assert.Equal(7, (await Availability(c, d, 60)).Slots.Length);
        Assert.Equal(7, (await Availability(c, d, 45)).Slots.Length);
        Assert.Empty((await Availability(c, d, 300)).Slots);
    }
    [Fact] public async Task DisponibilidadMultiplesBloques()
    {
        using var c = await Client(); var d = await Setup(multiple: true);
        var slots = (await Availability(c, d, 60)).Slots;
        Assert.Equal(10, slots.Length);
        Assert.Contains(slots, x => x.StartTime == new TimeOnly(14, 0));
        Assert.DoesNotContain(slots, x => x.StartTime >= new TimeOnly(12, 0) && x.StartTime < new TimeOnly(14, 0));
    }
    [Fact] public async Task DisponibilidadExcluyeDescansoYPermiteLimites()
    {
        using var c = await Client(); var d = await Setup(true);
        var slots = (await Availability(c, d)).Slots;
        Assert.Equal(7, slots.Length); Assert.DoesNotContain(slots, x => x.StartTime == new TimeOnly(10, 0));
        Assert.Contains(slots, x => x.EndTime == new TimeOnly(10, 0));
        Assert.Contains(slots, x => x.StartTime == new TimeOnly(10, 30));
        Assert.DoesNotContain((await Availability(c, d, 60)).Slots, x => x.StartTime == new TimeOnly(9, 30));
        await Create(c, d, 9, 10);
    }
    [Fact] public async Task DisponibilidadExcluyeCitas()
    {
        using var c = await Client(); var d = await Setup(); await Create(c, d);
        var slots = (await Availability(c, d)).Slots;
        Assert.Equal(6, slots.Length); Assert.All(slots, x => Assert.True(x.StartTime >= new TimeOnly(9, 0)));
    }
    [Theory][InlineData("CANCELLED")][InlineData("NO_SHOW")]
    public async Task EstadosNoBloqueantesLiberanYReactivacionRevalida(string status)
    {
        using var c = await Client(); var d = await Setup(); var a = await Create(c, d);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PatchAsJsonAsync($"{Path}/{a.Id}/status", new { status })).StatusCode);
        Assert.Equal(8, (await Availability(c, d)).Slots.Length);
        await Create(c, d);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PatchAsJsonAsync($"{Path}/{a.Id}/status", new { status = "CONFIRMED" })).StatusCode);
        Assert.Equal(status, (await c.GetFromJsonAsync<AppointmentResponse>($"{Path}/{a.Id}"))!.Status);
    }
    [Theory][InlineData("CONFIRMED")][InlineData("IN_PROGRESS")][InlineData("COMPLETED")]
    public async Task CambiosDeEstadoConservanOcupacion(string status)
    {
        using var c = await Client(); var d = await Setup(); var a = await Create(c, d);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PatchAsJsonAsync($"{Path}/{a.Id}/status", new { status })).StatusCode);
        Assert.Equal(status, (await c.GetFromJsonAsync<AppointmentResponse>($"{Path}/{a.Id}"))!.Status);
        Assert.Equal(6, (await Availability(c, d)).Slots.Length);
    }
    [Fact] public async Task EditarReprogramaSinSolaparseConsigoMisma()
    {
        using var c = await Client(); var d = await Setup(); var a = await Create(c, d);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"{Path}/{a.Id}", Body(d))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"{Path}/{a.Id}", Body(d, 9, 10))).StatusCode);
        Assert.Equal(new TimeOnly(9, 0), (await c.GetFromJsonAsync<AppointmentResponse>($"{Path}/{a.Id}"))!.StartTime);
        await Create(c, d);
        Assert.Equal(HttpStatusCode.Conflict, (await c.PutAsJsonAsync($"{Path}/{a.Id}", Body(d))).StatusCode);
    }
    [Fact] public async Task PermisoCancelarNoPermiteOtrosEstadosYUpdateNoCancela()
    {
        using var admin = await Client(); var d = await Setup(); var a = await Create(admin, d);
        using var update = await Client("appointments.update"); using var cancel = await Client("appointments.cancel");
        Assert.Equal(HttpStatusCode.Forbidden, (await update.PatchAsJsonAsync($"{Path}/{a.Id}/status", new { status = "CANCELLED" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cancel.PatchAsJsonAsync($"{Path}/{a.Id}/status", new { status = "CONFIRMED" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await update.PatchAsJsonAsync($"{Path}/{a.Id}/status", new { status = "CONFIRMED" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await cancel.PatchAsJsonAsync($"{Path}/{a.Id}/status", new { status = "CANCELLED" })).StatusCode);
    }
    [Fact] public async Task SinPermisosNoAccedeANingunEndpoint()
    {
        using var c = await Client("none"); var d = await Setup(); var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(Path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync($"{Path}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(AvailabilityUrl(d))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PostAsJsonAsync(Path, Body(d))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PutAsJsonAsync($"{Path}/{id}", Body(d))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.PatchAsJsonAsync($"{Path}/{id}/status", new { status = "CANCELLED" })).StatusCode);
    }
    [Fact] public async Task PermisosCrearYVerIndependientes()
    {
        using var create = await Client("appointments.create"); using var view = await Client("appointments.view"); var d = await Setup();
        var a = await Create(create, d);
        Assert.Equal(HttpStatusCode.OK, (await view.GetAsync($"{Path}/{a.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await view.GetAsync(AvailabilityUrl(d))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await view.PostAsJsonAsync(Path, Body(d, 9, 10))).StatusCode);
    }
    [Fact] public async Task AuditoriaNoIncluyeMotivoNiNotas()
    {
        using var c = await Client(); var d = await Setup(); var a = await Create(c, d);
        Assert.Equal(HttpStatusCode.OK, (await c.PutAsJsonAsync($"{Path}/{a.Id}", Body(d, 9, 10))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await c.PatchAsJsonAsync($"{Path}/{a.Id}/status", new { status = "CANCELLED" })).StatusCode);
        await Db(async db => {
            var logs = await db.AuditLogs.Where(x => x.EntityId == a.Id).ToArrayAsync();
            foreach (var action in new[] { "appointment_created", "appointment_updated", "appointment_status_changed", "appointment_cancelled" })
                Assert.Contains(logs, x => x.Action == action && x.UserId != null);
            Assert.All(logs, x => { Assert.DoesNotContain("privad", x.MetadataJson ?? ""); Assert.DoesNotContain("Reason", x.MetadataJson ?? ""); });
        });
    }
    [Fact] public async Task DisponibilidadSinHorarioOInactivoVacia()
    {
        using var c = await Client(); var d = await Setup();
        Assert.Empty((await c.GetFromJsonAsync<AvailabilityResponse>(AvailabilityUrl(d, date: "2030-01-08")))!.Slots);
        await Db(async db => { (await db.DoctorSchedules.SingleAsync(x => x.DoctorId == d.Doctor)).IsActive = false; });
        Assert.Empty((await Availability(c, d)).Slots);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PostAsJsonAsync(Path, Body(d))).StatusCode);
    }
    [Theory][InlineData(0)][InlineData(-1)][InlineData(1441)]
    public async Task DuracionInvalida400(int duration)
    {
        using var c = await Client(); var d = await Setup();
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync(AvailabilityUrl(d, duration))).StatusCode);
    }
    [Fact] public async Task Errores404Y400YSinAutenticacion()
    {
        using var c = await Client(); var d = await Setup(); var id = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync($"{Path}/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync($"{Path}/{id}", Body(d))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await c.PostAsJsonAsync(Path, Body(d with { Patient = id }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync(Path + "?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.GetAsync(Path + "/availability")).StatusCode);
        var a = await Create(c, d);
        Assert.Equal(HttpStatusCode.BadRequest, (await c.PatchAsJsonAsync($"{Path}/{a.Id}/status", new { status = "UNKNOWN" })).StatusCode);
        using var anonymous = f.Client(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Path)).StatusCode);
    }
    [Fact] public async Task SolapamientoEntreSucursalesTambienBloqueaDisponibilidad()
    {
        using var c = await Client(); var d = await Setup(); var other = await Setup();
        await Db(db => {
            db.DoctorCenters.Add(new DoctorCenter { DoctorId = d.Doctor, CenterId = other.Center });
            db.DoctorSchedules.Add(new DoctorSchedule { DoctorId = d.Doctor, CenterId = other.Center, DayOfWeek = 1, StartTime = new(8, 0), EndTime = new(12, 0) });
            return Task.CompletedTask;
        });
        await Create(c, d); var secondCenter = d with { Center = other.Center };
        Assert.Equal(HttpStatusCode.Conflict, (await c.PostAsJsonAsync(Path, Body(secondCenter))).StatusCode);
        Assert.Equal(6, (await Availability(c, secondCenter)).Slots.Length);
    }

    [Fact] public async Task AgendaIncludesEmptyDoctorsSchedulesBreaksAndCorrectAppointments()
    {
        using var c=await Client();var d=await Setup(true,true);var a=await Create(c,d);
        using var viewer=await Client("appointments.view");
        var result=(await viewer.GetFromJsonAsync<AgendaResponse>($"{Path}/agenda?date=2030-01-07&doctorId={d.Doctor}"))!;
        Assert.Single(result.Doctors);Assert.Equal(d.Doctor,result.Doctors[0].Id);
        Assert.Equal(2,result.WorkingIntervals.Length);Assert.Equal(new TimeOnly(10,0),result.WorkingIntervals[0].BreakStart);
        Assert.Equal(30,result.SlotMinutes);Assert.Equal(a,result.Appointments.Single());
        var empty=(await viewer.GetFromJsonAsync<AgendaResponse>($"{Path}/agenda?date=2030-01-14&doctorId={d.Doctor}"))!;
        Assert.Single(empty.Doctors);Assert.Empty(empty.Appointments);Assert.Equal(2,empty.WorkingIntervals.Length);
        var off=(await viewer.GetFromJsonAsync<AgendaResponse>($"{Path}/agenda?date=2030-01-08&doctorId={d.Doctor}"))!;
        Assert.Empty(off.WorkingIntervals);Assert.Empty(off.Appointments);
    }
    [Theory]
    [InlineData("centerId")][InlineData("doctorId")][InlineData("specialtyId")]
    public async Task AgendaFiltersDoNotMixDoctorsOrCenters(string filter)
    {
        using var c=await Client();var d=await Setup();var other=await Setup();await Create(c,d);await Create(c,other);
        var id=filter=="centerId"?d.Center:filter=="doctorId"?d.Doctor:d.Specialty;
        var result=(await c.GetFromJsonAsync<AgendaResponse>($"{Path}/agenda?date=2030-01-07&{filter}={id}"))!;
        Assert.Single(result.Doctors);Assert.Equal(d.Doctor,result.Doctors[0].Id);
        Assert.Single(result.Appointments);Assert.All(result.WorkingIntervals,w=>Assert.Equal(d.Doctor,w.DoctorId));
    }
    [Fact] public async Task AgendaStatusFilterRetainsWorkingIntervals()
    {
        using var c=await Client();var d=await Setup();await Create(c,d);
        var result=(await c.GetFromJsonAsync<AgendaResponse>($"{Path}/agenda?date=2030-01-07&doctorId={d.Doctor}&status=CONFIRMED"))!;
        Assert.Empty(result.Appointments);Assert.Single(result.WorkingIntervals);Assert.Single(result.Doctors);
    }
    [Fact] public async Task AgendaRejectsMissingDateAndInvalidStatus()
    {
        using var c=await Client();
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync($"{Path}/agenda")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync($"{Path}/agenda?date=2030-01-07&status=INVALID")).StatusCode);
    }
    [Fact] public async Task AgendaRequiresGlobalPermission()
    {
        using var c=await Client("doctors.view");
        Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync($"{Path}/agenda?date=2030-01-07")).StatusCode);
    }
    [Fact] public async Task AgendaExcludesInactiveSchedules()
    {
        using var c=await Client();var d=await Setup();
        await Db(async db=>{foreach(var schedule in await db.DoctorSchedules.Where(s=>s.DoctorId==d.Doctor).ToArrayAsync())schedule.IsActive=false;});
        var result=(await c.GetFromJsonAsync<AgendaResponse>($"{Path}/agenda?date=2030-01-07&doctorId={d.Doctor}"))!;
        Assert.Empty(result.WorkingIntervals);
    }
}
