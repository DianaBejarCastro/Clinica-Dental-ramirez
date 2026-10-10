using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ClinicaRamirez.Api.Tests;

public sealed class ClinicAdministrationTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N");
    private static Guid Id(JsonElement value) => value.GetProperty("id").GetGuid();

    private async Task<HttpClient> Admin()
    {
        var client = f.Client();
        var login = await f.Login(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Body.AccessToken);
        return client;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        using (response)
        {
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == status, $"Esperado {(int)status}; recibido {(int)response.StatusCode}: {content}");
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
    }

    private static async Task Status(HttpResponseMessage response, HttpStatusCode status)
    {
        using (response)
            Assert.True(response.StatusCode == status,
                $"Esperado {(int)status}; recibido {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static Dictionary<string, object?> CenterBody(string? code = null, bool isMain = false) => new()
    {
        ["name"] = Unique("Sucursal "), ["code"] = code ?? Unique("CENTER_"),
        ["address"] = "Avenida Integracion 100", ["country"] = "Bolivia", ["city"] = "La Paz",
        ["department"] = "La Paz", ["phone"] = "22000000", ["isMain"] = isMain, ["isActive"] = true
    };

    private static Dictionary<string, object?> SpecialtyBody(string? code = null, string? name = null) => new()
    {
        ["code"] = code ?? Unique("SPECIALTY_"), ["name"] = name ?? Unique("Especialidad "),
        ["description"] = "Datos sinteticos de integracion", ["isActive"] = true
    };

    private Dictionary<string, object?> DoctorBody(string? license = null) => new()
    {
        ["newUser"] = new { firstName = "Doctor", lastName = "Integracion", email = Unique("doctor") + "@example.com", password = f.Password },
        ["professionalLicense"] = license ?? Unique("LICENSE_"), ["ci"] = "TEST-CI",
        ["birthDate"] = "1990-06-15", ["gender"] = "No especificado", ["phone"] = "72000000",
        ["address"] = "Direccion sintetica", ["bio"] = "Biografia sintetica", ["isActive"] = true
    };

    private static Dictionary<string, object?> ScheduleBody(Guid centerId, string start = "08:00:00", string end = "12:00:00", int day = 1) => new()
    {
        ["centerId"] = centerId, ["dayOfWeek"] = day, ["startTime"] = start, ["endTime"] = end,
        ["hasBreak"] = false, ["breakStart"] = null, ["breakEnd"] = null, ["isActive"] = true
    };

    private static Task<JsonElement> CreateCenter(HttpClient client, bool main = false) =>
        SendCreated(client, "/api/admin/centers", CenterBody(isMain: main));
    private static Task<JsonElement> CreateSpecialty(HttpClient client) =>
        SendCreated(client, "/api/admin/specialties", SpecialtyBody());
    private Task<JsonElement> CreateDoctor(HttpClient client) =>
        SendCreated(client, "/api/admin/doctors", DoctorBody());
    private static async Task<JsonElement> SendCreated(HttpClient client, string path, object body) =>
        await Json(await client.PostAsJsonAsync(path, body), HttpStatusCode.Created);

    private async Task<(Guid Doctor, Guid Central, Guid North)> ScheduleContext(HttpClient client)
    {
        var doctor = Id(await CreateDoctor(client));
        var central = Id(await CreateCenter(client));
        var north = Id(await CreateCenter(client));
        await Status(await client.PutAsJsonAsync($"/api/admin/doctors/{doctor}/centers", new { centerIds = new[] { central, north } }), HttpStatusCode.NoContent);
        return (doctor, central, north);
    }

    [Fact]
    public async Task ClinicGetDevuelveUnaClinicaConTimestampsUtc()
    {
        using var client = await Admin();
        var clinic = await Json(await client.GetAsync("/api/admin/clinic"));
        Assert.NotEqual(Guid.Empty, Id(clinic));
        Assert.False(string.IsNullOrWhiteSpace(clinic.GetProperty("name").GetString()));
        Assert.Equal(DateTimeKind.Utc, clinic.GetProperty("createdAt").GetDateTime().Kind);
        using var scope = f.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Clinics.CountAsync());
    }

    [Fact]
    public async Task ClinicUpdateConservaIdentidadYAudita()
    {
        using var client = await Admin();
        var original = await Json(await client.GetAsync("/api/admin/clinic"));
        var name = Unique("Clinica actualizada ");
        var body = new { name, legalName = "Entidad sintetica", description = "Prueba", logoUrl = "https://example.com/logo.png", phone = "22123456", whatsapp = "72000000", email = "clinic@example.com", taxId = "TEST-NIT", isActive = true };
        var updated = await Json(await client.PutAsJsonAsync("/api/admin/clinic", body));
        Assert.False(original.TryGetProperty("website", out _));
        Assert.False(updated.TryGetProperty("website", out _));
        Assert.False(updated.TryGetProperty("siteWeb", out _));
        Assert.Equal(Id(original), Id(updated));
        Assert.Equal(name, updated.GetProperty("name").GetString());
        Assert.Equal("TEST-NIT", (await Json(await client.GetAsync("/api/admin/clinic"))).GetProperty("taxId").GetString());
        await AuditContains("clinic_updated", Id(original));
    }

    [Fact]
    public async Task CenterCrearConsultarEditarYDesactivarConservaRegistro()
    {
        using var client = await Admin();
        var created = await CreateCenter(client);
        var id = Id(created);
        var body = CenterBody(created.GetProperty("code").GetString());
        body["name"] = "Sucursal actualizada";
        body["latitude"] = -16.5m; body["longitude"] = -68.15m;
        var updated = await Json(await client.PutAsJsonAsync($"/api/admin/centers/{id}", body));
        Assert.Equal("Sucursal actualizada", updated.GetProperty("name").GetString());
        Assert.Equal(-16.5m, updated.GetProperty("latitude").GetDecimal());
        await Status(await client.PatchAsJsonAsync($"/api/admin/centers/{id}/status", new { isActive = false }), HttpStatusCode.NoContent);
        var detail = await Json(await client.GetAsync($"/api/admin/centers/{id}"));
        Assert.False(detail.GetProperty("isActive").GetBoolean());
        Assert.Equal(Id(created), Id(detail));
        foreach (var action in new[] { "center_created", "center_updated", "center_status_changed" }) await AuditContains(action, id);
    }

    [Fact]
    public async Task CenterCodeDuplicadoDevuelveConflicto()
    {
        using var client = await Admin();
        var created = await CreateCenter(client);
        await Status(await client.PostAsJsonAsync("/api/admin/centers", CenterBody(created.GetProperty("code").GetString())), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CenterSoloPermiteUnaPrincipalActiva()
    {
        using var client = await Admin();
        var main = await CreateCenter(client, true);
        await Status(await client.PostAsJsonAsync("/api/admin/centers", CenterBody(isMain: true)), HttpStatusCode.Conflict);
        await Status(await client.PatchAsJsonAsync($"/api/admin/centers/{Id(main)}/status", new { isActive = false }), HttpStatusCode.NoContent);
        var replacement = await CreateCenter(client, true);
        await Status(await client.PatchAsJsonAsync($"/api/admin/centers/{Id(main)}/status", new { isActive = true }), HttpStatusCode.Conflict);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Centers.CountAsync(x => x.IsActive && x.IsMain));
        await Status(await client.PatchAsJsonAsync($"/api/admin/centers/{Id(replacement)}/status", new { isActive = false }), HttpStatusCode.NoContent);
    }

    [Theory]
    [InlineData(91, 0)] [InlineData(-91, 0)] [InlineData(0, 181)] [InlineData(0, -181)]
    public async Task CenterRechazaCoordenadasFueraDeRango(decimal latitude, decimal longitude)
    {
        using var client = await Admin();
        var body = CenterBody(); body["latitude"] = latitude; body["longitude"] = longitude;
        await Status(await client.PostAsJsonAsync("/api/admin/centers", body), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ClinicYSpecialtySeedIdempotenteNoCreaDoctores()
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var specialties = await db.Specialties.CountAsync();
        var doctors = await db.DoctorProfiles.CountAsync();
        var seed = scope.ServiceProvider.GetRequiredService<ClinicSeed>();
        await seed.Run(); await seed.Run();
        Assert.Equal(1, await db.Clinics.CountAsync());
        Assert.Equal(specialties, await db.Specialties.CountAsync());
        Assert.Equal(doctors, await db.DoctorProfiles.CountAsync());
        string[] codes = ["GENERAL_DENTISTRY", "ORTHODONTICS", "PEDIATRIC_DENTISTRY", "ENDODONTICS", "PERIODONTICS", "ORAL_MAXILLOFACIAL_SURGERY", "PROSTHODONTICS", "ORAL_PATHOLOGY", "ORAL_MAXILLOFACIAL_RADIOLOGY", "PUBLIC_HEALTH_DENTISTRY", "IMPLANTOLOGY", "AESTHETIC_DENTISTRY"];
        Assert.Equal(12, await db.Specialties.CountAsync(x => codes.Contains(x.Code)));
    }

    [Fact]
    public async Task SpecialtyCrearActualizarDesactivarYAuditar()
    {
        using var client = await Admin();
        var created = await CreateSpecialty(client);
        var id = Id(created);
        var body = SpecialtyBody(created.GetProperty("code").GetString());
        body["description"] = "Descripcion actualizada";
        var updated = await Json(await client.PutAsJsonAsync($"/api/admin/specialties/{id}", body));
        Assert.Equal("Descripcion actualizada", updated.GetProperty("description").GetString());
        await Status(await client.PatchAsJsonAsync($"/api/admin/specialties/{id}/status", new { isActive = false }), HttpStatusCode.NoContent);
        Assert.False((await Json(await client.GetAsync($"/api/admin/specialties/{id}"))).GetProperty("isActive").GetBoolean());
        foreach (var action in new[] { "specialty_created", "specialty_updated", "specialty_status_changed" }) await AuditContains(action, id);
    }

    [Theory]
    [InlineData("code")] [InlineData("name")]
    public async Task SpecialtyRechazaCodigoONombreDuplicado(string field)
    {
        using var client = await Admin();
        var created = await CreateSpecialty(client);
        var duplicate = SpecialtyBody(); duplicate[field] = created.GetProperty(field).GetString();
        await Status(await client.PostAsJsonAsync("/api/admin/specialties", duplicate), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DoctorNuevoCreaUnUserConRolDoctorSinExponerCredenciales()
    {
        using var client = await Admin();
        var body = DoctorBody();
        var created = await SendCreated(client, "/api/admin/doctors", body);
        var userId = created.GetProperty("userId").GetGuid();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.UserRoles.AnyAsync(x => x.UserId == userId && x.Role.Code == "DOCTOR"));
        Assert.Equal(1, await db.DoctorProfiles.CountAsync(x => x.UserId == userId));
        Assert.DoesNotContain("password", created.ToString().ToLowerInvariant());
        Assert.DoesNotContain(f.Password, created.ToString());
        await AuditContains("doctor_created", Id(created));
    }

    [Fact]
    public async Task DoctorVinculaUserExistenteSinRolYAgregaDoctorSinDuplicarUser()
    {
        var email = await f.CreateUser();
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        var count = await db.Users.CountAsync();
        Assert.False(await db.UserRoles.AnyAsync(x => x.UserId == user.Id));
        using var client = await Admin();
        var body = new { userId = user.Id, professionalLicense = Unique("EXISTING_"), isActive = true };
        var doctor = await SendCreated(client, "/api/admin/doctors", body);
        Assert.Equal(user.Id, doctor.GetProperty("userId").GetGuid());
        Assert.Equal(count, await db.Users.CountAsync());
        Assert.True(await db.UserRoles.AnyAsync(x => x.UserId == user.Id && x.Role.Code == "DOCTOR"));
        await Status(await client.PostAsJsonAsync("/api/admin/doctors", body), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DoctorProfessionalLicenseDuplicadoNoDejaUserHuerfano()
    {
        using var client = await Admin();
        var created = await CreateDoctor(client);
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await db.Users.CountAsync();
        await Status(await client.PostAsJsonAsync("/api/admin/doctors", DoctorBody(created.GetProperty("professionalLicense").GetString())), HttpStatusCode.Conflict);
        Assert.Equal(users, await db.Users.CountAsync());
    }

    [Fact]
    public async Task DoctorConPerfilNoPermiteRetirarRolDoctorDelUser()
    {
        using var client = await Admin();
        var doctor = await CreateDoctor(client);
        var userId = doctor.GetProperty("userId").GetGuid();
        await Status(await client.PutAsJsonAsync($"/api/admin/users/{userId}/roles", new { roleIds = Array.Empty<Guid>() }), HttpStatusCode.Conflict);
        using var scope = f.Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().UserRoles.AnyAsync(x => x.UserId == userId && x.Role.Code == "DOCTOR"));
    }

    [Fact]
    public async Task DoctorConPerfilImpideDesactivarRolDoctor()
    {
        using var client = await Admin();
        await CreateDoctor(client);
        var roles = await Json(await client.GetAsync("/api/admin/roles"));
        var role = roles.EnumerateArray().Single(x => x.GetProperty("code").GetString() == "DOCTOR");
        await Status(await client.PutAsJsonAsync($"/api/admin/roles/{Id(role)}", new { name = role.GetProperty("name").GetString(), isActive = false }), HttpStatusCode.Conflict);
        Assert.True((await Json(await client.GetAsync($"/api/admin/roles/{Id(role)}"))).GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DoctorActualizaDatosProfesionalesYDesactivaSinDesactivarUser()
    {
        using var client = await Admin();
        var doctor = await CreateDoctor(client); var id = Id(doctor);
        var body = new { professionalLicense = doctor.GetProperty("professionalLicense").GetString(), ci = "CI-UPDATED", bio = "Actualizacion profesional", birthDate = "1992-03-04", isActive = true };
        var updated = await Json(await client.PutAsJsonAsync($"/api/admin/doctors/{id}", body));
        Assert.Equal("CI-UPDATED", updated.GetProperty("ci").GetString());
        await Status(await client.PatchAsJsonAsync($"/api/admin/doctors/{id}/status", new { isActive = false }), HttpStatusCode.NoContent);
        Assert.False((await Json(await client.GetAsync($"/api/admin/doctors/{id}"))).GetProperty("isActive").GetBoolean());
        using var scope = f.Services.CreateScope();
        var userId = doctor.GetProperty("userId").GetGuid();
        Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(x => x.Id == userId && x.IsActive));
        foreach (var action in new[] { "doctor_updated", "doctor_status_changed" }) await AuditContains(action, id);
    }

    [Fact]
    public async Task DoctorAsignaDosSpecialtiesEliminaDuplicadosYSustituyeRelacionalmente()
    {
        using var client = await Admin();
        var doctorId = Id(await CreateDoctor(client));
        var a = Id(await CreateSpecialty(client)); var b = Id(await CreateSpecialty(client));
        var path = $"/api/admin/doctors/{doctorId}/specialties";
        await Status(await client.PutAsJsonAsync(path, new { specialtyIds = new[] { a, b, a } }), HttpStatusCode.NoContent);
        var assignments = await Json(await client.GetAsync(path));
        Assert.Equal(2, assignments.GetArrayLength());
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.DoctorSpecialties.CountAsync(x => x.DoctorId == doctorId));
        Assert.Equal(1, (await Json(await client.GetAsync($"/api/admin/specialties/{a}"))).GetProperty("doctorCount").GetInt32());
        await Status(await client.PutAsJsonAsync(path, new { specialtyIds = new[] { b } }), HttpStatusCode.NoContent);
        Assert.Equal(b, Id((await Json(await client.GetAsync(path)))[0]));
        Assert.Equal(1, await db.DoctorSpecialties.CountAsync(x => x.DoctorId == doctorId));
        await AuditContains("doctor_specialties_updated", doctorId);
    }

    [Fact]
    public async Task DoctorRechazaSpecialtyInactivaSinPerderAsignaciones()
    {
        using var client = await Admin();
        var doctorId = Id(await CreateDoctor(client));
        var active = Id(await CreateSpecialty(client)); var inactive = Id(await CreateSpecialty(client));
        var path = $"/api/admin/doctors/{doctorId}/specialties";
        await Status(await client.PutAsJsonAsync(path, new { specialtyIds = new[] { active } }), HttpStatusCode.NoContent);
        await Status(await client.PatchAsJsonAsync($"/api/admin/specialties/{inactive}/status", new { isActive = false }), HttpStatusCode.NoContent);
        await Status(await client.PutAsJsonAsync(path, new { specialtyIds = new[] { inactive } }), HttpStatusCode.BadRequest);
        Assert.Equal(active, Id((await Json(await client.GetAsync(path)))[0]));
    }

    [Fact]
    public async Task DoctorAsignaDosCentersSinDuplicadosEnTablaPuente()
    {
        using var client = await Admin();
        var (doctor, central, north) = await ScheduleContext(client);
        var path = $"/api/admin/doctors/{doctor}/centers";
        await Status(await client.PutAsJsonAsync(path, new { centerIds = new[] { central, north, central } }), HttpStatusCode.NoContent);
        Assert.Equal(2, (await Json(await client.GetAsync(path))).GetArrayLength());
        using var scope = f.Services.CreateScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<AppDbContext>().DoctorCenters.CountAsync(x => x.DoctorId == doctor && x.IsActive));
        await AuditContains("doctor_centers_updated", doctor);
    }

    [Fact]
    public async Task DoctorRechazaCenterInactivo()
    {
        using var client = await Admin();
        var doctor = Id(await CreateDoctor(client)); var center = Id(await CreateCenter(client));
        await Status(await client.PatchAsJsonAsync($"/api/admin/centers/{center}/status", new { isActive = false }), HttpStatusCode.NoContent);
        await Status(await client.PutAsJsonAsync($"/api/admin/doctors/{doctor}/centers", new { centerIds = new[] { center } }), HttpStatusCode.BadRequest);
        Assert.Equal(0, (await Json(await client.GetAsync($"/api/admin/doctors/{doctor}/centers"))).GetArrayLength());
    }

    [Fact]
    public async Task DoctorInactivoRechazaNuevasSpecialties()
    {
        using var client = await Admin();
        var doctor = Id(await CreateDoctor(client)); var specialty = Id(await CreateSpecialty(client));
        await Status(await client.PatchAsJsonAsync($"/api/admin/doctors/{doctor}/status", new { isActive = false }), HttpStatusCode.NoContent);
        await Status(await client.PutAsJsonAsync($"/api/admin/doctors/{doctor}/specialties", new { specialtyIds = new[] { specialty } }), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ScheduleValidoSinBreakNormalizaCamposYPermiteEditarDesactivar()
    {
        using var client = await Admin();
        var (doctor, center, _) = await ScheduleContext(client);
        var path = $"/api/admin/doctors/{doctor}/schedules";
        var body = ScheduleBody(center); body["breakStart"] = "09:00:00"; body["breakEnd"] = "10:00:00";
        var created = await SendCreated(client, path, body);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("breakStart").ValueKind);
        Assert.Equal(JsonValueKind.Null, created.GetProperty("breakEnd").ValueKind);
        Assert.Equal(doctor, created.GetProperty("doctorId").GetGuid());
        Assert.Equal(center, created.GetProperty("centerId").GetGuid());
        var updated = await Json(await client.PutAsJsonAsync($"{path}/{Id(created)}", ScheduleBody(center, "09:00:00", "13:00:00")));
        Assert.StartsWith("09:00", updated.GetProperty("startTime").GetString());
        await Status(await client.PatchAsJsonAsync($"{path}/{Id(created)}/status", new { isActive = false }), HttpStatusCode.NoContent);
        var list = await Json(await client.GetAsync(path));
        Assert.False(list.EnumerateArray().Single(x => Id(x) == Id(created)).GetProperty("isActive").GetBoolean());
        foreach (var action in new[] { "schedule_created", "schedule_updated", "schedule_status_changed" }) await AuditContains(action, Id(created));
    }

    [Fact]
    public async Task SchedulePermiteBreakEstrictamenteInterior()
    {
        using var client = await Admin();
        var (doctor, center, _) = await ScheduleContext(client);
        var body = ScheduleBody(center, "08:00:00", "18:00:00");
        body["hasBreak"] = true; body["breakStart"] = "12:00:00"; body["breakEnd"] = "13:00:00";
        var created = await SendCreated(client, $"/api/admin/doctors/{doctor}/schedules", body);
        Assert.True(created.GetProperty("hasBreak").GetBoolean());
        Assert.StartsWith("12:00", created.GetProperty("breakStart").GetString());
    }

    [Theory]
    [InlineData(null, null)] [InlineData("08:00:00", "09:00:00")]
    [InlineData("09:00:00", "12:00:00")] [InlineData("10:00:00", "09:00:00")]
    [InlineData("09:00:00", "09:00:00")] [InlineData("09:00:00", null)]
    public async Task ScheduleRechazaBreakInvalido(string? start, string? end)
    {
        using var client = await Admin();
        var (doctor, center, _) = await ScheduleContext(client);
        var body = ScheduleBody(center); body["hasBreak"] = true; body["breakStart"] = start; body["breakEnd"] = end;
        await Status(await client.PostAsJsonAsync($"/api/admin/doctors/{doctor}/schedules", body), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("12:00:00", "12:00:00")] [InlineData("18:00:00", "08:00:00")]
    public async Task ScheduleRechazaStartMayorOIgualEnd(string start, string end)
    {
        using var client = await Admin();
        var (doctor, center, _) = await ScheduleContext(client);
        await Status(await client.PostAsJsonAsync($"/api/admin/doctors/{doctor}/schedules", ScheduleBody(center, start, end)), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ScheduleRechazaSolapamientoMismoDoctorEnCualquierCenter(bool differentCenter)
    {
        using var client = await Admin();
        var (doctor, central, north) = await ScheduleContext(client);
        var path = $"/api/admin/doctors/{doctor}/schedules";
        await SendCreated(client, path, ScheduleBody(central));
        await Status(await client.PostAsJsonAsync(path, ScheduleBody(differentCenter ? north : central, "11:00:00", "15:00:00")), HttpStatusCode.Conflict);
        Assert.Equal(1, (await Json(await client.GetAsync(path))).GetArrayLength());
    }

    [Fact]
    public async Task ScheduleRechazaCenterSinAsignacion()
    {
        using var client = await Admin();
        var doctor = Id(await CreateDoctor(client)); var center = Id(await CreateCenter(client));
        await Status(await client.PostAsJsonAsync($"/api/admin/doctors/{doctor}/schedules", ScheduleBody(center)), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SchedulePermiteMultiplesBloquesAdyacentesYOtroDia()
    {
        using var client = await Admin();
        var (doctor, central, north) = await ScheduleContext(client);
        var path = $"/api/admin/doctors/{doctor}/schedules";
        await SendCreated(client, path, ScheduleBody(central));
        await SendCreated(client, path, ScheduleBody(north, "12:00:00", "14:00:00"));
        await SendCreated(client, path, ScheduleBody(north, "14:00:00", "18:00:00"));
        await SendCreated(client, path, ScheduleBody(central, day: 2));
        Assert.Equal(4, (await Json(await client.GetAsync(path))).GetArrayLength());
    }

    [Fact]
    public async Task ScheduleConcurrenteEntreCentersNoPermiteSolapamiento()
    {
        using var client = await Admin();
        var (doctor, central, north) = await ScheduleContext(client);
        var path = $"/api/admin/doctors/{doctor}/schedules";
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync(path, ScheduleBody(central)),
            client.PostAsJsonAsync(path, ScheduleBody(north, "10:00:00", "15:00:00")));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        Assert.Equal(1, (await Json(await client.GetAsync(path))).GetArrayLength());
    }

    [Fact]
    public async Task ScheduleDesactivadoNoBloqueaPeroReactivacionValidaSolapamiento()
    {
        using var client = await Admin();
        var (doctor, central, north) = await ScheduleContext(client);
        var path = $"/api/admin/doctors/{doctor}/schedules";
        var first = await SendCreated(client, path, ScheduleBody(central));
        await Status(await client.PatchAsJsonAsync($"{path}/{Id(first)}/status", new { isActive = false }), HttpStatusCode.NoContent);
        await SendCreated(client, path, ScheduleBody(north));
        await Status(await client.PatchAsJsonAsync($"{path}/{Id(first)}/status", new { isActive = true }), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ScheduleEditarNoPuedeIntroducirSolapamientoEntreCenters()
    {
        using var client = await Admin();
        var (doctor, central, north) = await ScheduleContext(client);
        var path = $"/api/admin/doctors/{doctor}/schedules";
        await SendCreated(client, path, ScheduleBody(central));
        var second = await SendCreated(client, path, ScheduleBody(north, "14:00:00", "18:00:00"));
        await Status(await client.PutAsJsonAsync($"{path}/{Id(second)}", ScheduleBody(north, "11:00:00", "15:00:00")), HttpStatusCode.Conflict);
        var persisted = (await Json(await client.GetAsync(path))).EnumerateArray().Single(x => Id(x) == Id(second));
        Assert.StartsWith("14:00", persisted.GetProperty("startTime").GetString());
    }

    [Theory]
    [InlineData(0)] [InlineData(8)]
    public async Task ScheduleRechazaDiaFueraDeSemanaIso(int day)
    {
        using var client = await Admin();
        var (doctor, center, _) = await ScheduleContext(client);
        await Status(await client.PostAsJsonAsync($"/api/admin/doctors/{doctor}/schedules", ScheduleBody(center, day: day)), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("doctors")] [InlineData("centers")]
    public async Task DesactivarDoctorOCenterDesactivaHorariosSinReactivarlosImplicitamente(string resource)
    {
        using var client = await Admin();
        var (doctor, central, _) = await ScheduleContext(client);
        var path = $"/api/admin/doctors/{doctor}/schedules";
        await SendCreated(client, path, ScheduleBody(central));
        var target = resource == "doctors" ? doctor : central;
        await Status(await client.PatchAsJsonAsync($"/api/admin/{resource}/{target}/status", new { isActive = false }), HttpStatusCode.NoContent);
        Assert.False((await Json(await client.GetAsync(path)))[0].GetProperty("isActive").GetBoolean());
        await Status(await client.PatchAsJsonAsync($"/api/admin/{resource}/{target}/status", new { isActive = true }), HttpStatusCode.NoContent);
        Assert.False((await Json(await client.GetAsync(path)))[0].GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task DesasignarCenterDesactivaHorarioYSuReactivacionEsRechazada()
    {
        using var client = await Admin();
        var (doctor, central, north) = await ScheduleContext(client);
        var path = $"/api/admin/doctors/{doctor}/schedules";
        var schedule = await SendCreated(client, path, ScheduleBody(central));
        await Status(await client.PutAsJsonAsync($"/api/admin/doctors/{doctor}/centers", new { centerIds = new[] { north } }), HttpStatusCode.NoContent);
        Assert.False((await Json(await client.GetAsync(path)))[0].GetProperty("isActive").GetBoolean());
        await Status(await client.PatchAsJsonAsync($"{path}/{Id(schedule)}/status", new { isActive = true }), HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("clinic")] [InlineData("centers")] [InlineData("doctors")] [InlineData("specialties")] [InlineData("dashboard")]
    public async Task AdministracionExigeTokenYPermiso(string endpoint)
    {
        var path = "/api/admin/" + endpoint;
        using var anonymous = f.Client();
        await Status(await anonymous.GetAsync(path), HttpStatusCode.Unauthorized);
        using var unprivileged = f.Client();
        var login = await f.Login(unprivileged, await f.CreateUser());
        unprivileged.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        await Status(await unprivileged.GetAsync(path), HttpStatusCode.Forbidden);
        using var admin = await Admin();
        await Status(await admin.GetAsync(path), HttpStatusCode.OK);
    }

    [Fact]
    public async Task SchedulesExigeTokenYPermiso()
    {
        using var admin = await Admin();
        var doctor = Id(await CreateDoctor(admin));
        var path = $"/api/admin/doctors/{doctor}/schedules";
        using var anonymous = f.Client();
        await Status(await anonymous.GetAsync(path), HttpStatusCode.Unauthorized);
        using var unprivileged = f.Client();
        var login = await f.Login(unprivileged, await f.CreateUser());
        unprivileged.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        await Status(await unprivileged.GetAsync(path), HttpStatusCode.Forbidden);
        await Status(await admin.GetAsync(path), HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("centers")] [InlineData("doctors")] [InlineData("specialties")] [InlineData("schedules")]
    public async Task UpdateSinPermisoDisableNoPuedeCambiarEstadoMediantePut(string resource)
    {
        using var admin = await Admin();
        Dictionary<string, object?> body;
        string path;
        switch (resource)
        {
            case "centers":
                body = CenterBody();
                path = $"/api/admin/centers/{Id(await SendCreated(admin, "/api/admin/centers", body))}";
                break;
            case "doctors":
                body = DoctorBody();
                path = $"/api/admin/doctors/{Id(await SendCreated(admin, "/api/admin/doctors", body))}";
                body.Remove("newUser");
                break;
            case "specialties":
                body = SpecialtyBody();
                path = $"/api/admin/specialties/{Id(await SendCreated(admin, "/api/admin/specialties", body))}";
                break;
            default:
                var (doctor, center, _) = await ScheduleContext(admin);
                body = ScheduleBody(center);
                var created = await SendCreated(admin, $"/api/admin/doctors/{doctor}/schedules", body);
                path = $"/api/admin/doctors/{doctor}/schedules/{Id(created)}";
                break;
        }

        using var editor = f.Client();
        var login = await f.Login(editor, await f.CreateUser());
        editor.DefaultRequestHeaders.Authorization = new("Bearer", login.Body.AccessToken);
        var role = await SendCreated(admin, "/api/admin/roles", new { code = Unique("EDIT_ONLY_").ToUpperInvariant(), name = Unique("Editor limitado ") });
        var permissions = await Json(await admin.GetAsync("/api/admin/permissions"));
        var permissionIds = permissions.EnumerateArray()
            .Where(x => x.GetProperty("code").GetString() == resource + ".view" || x.GetProperty("code").GetString() == resource + ".update")
            .Select(Id).ToArray();
        Assert.Equal(2, permissionIds.Length);
        await Status(await admin.PutAsJsonAsync($"/api/admin/roles/{Id(role)}/permissions", new { permissionIds }), HttpStatusCode.NoContent);
        await Status(await admin.PutAsJsonAsync($"/api/admin/users/{login.Body.User.Id}/roles", new { roleIds = new[] { Id(role) } }), HttpStatusCode.NoContent);
        await Status(await editor.PutAsJsonAsync(path, body), HttpStatusCode.OK);
        body["isActive"] = false;
        await Status(await editor.PutAsJsonAsync(path, body), HttpStatusCode.Forbidden);
        await Status(await editor.PatchAsJsonAsync(path + "/status", new { isActive = false }), HttpStatusCode.Forbidden);
    }

    private async Task AuditContains(string action, Guid entityId)
    {
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.AuditLogs.Where(x => x.Action == action && x.EntityId == entityId).ToArrayAsync();
        Assert.NotEmpty(entries);
        foreach (var entry in entries)
        {
            Assert.NotNull(entry.UserId);
            var metadata = (entry.MetadataJson ?? "").ToLowerInvariant();
            Assert.DoesNotContain("password", metadata);
            Assert.DoesNotContain("token", metadata);
            Assert.DoesNotContain(f.Password.ToLowerInvariant(), metadata);
            Assert.DoesNotContain("test-ci", metadata);
            Assert.DoesNotContain("biografia sintetica", metadata);
        }
    }
}
