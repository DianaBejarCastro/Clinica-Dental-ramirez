using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.DTOs.Cashier;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClinicaRamirez.Api.Tests;
public sealed class CashierTests(IdentityFixture f):IClassFixture<IdentityFixture>
{
    private sealed record Data(HttpClient Client,Guid Actor,Guid Patient,Guid Plan,string DocumentNumber,string Email);
    private async Task<(HttpClient,Guid)> Login(string role="CASHIER")
    {
        var email=await f.CreateUser();using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<AppDbContext>();var user=await db.Users.SingleAsync(x=>x.Email==email);
        if(role!="NONE")db.UserRoles.Add(new(){UserId=user.Id,RoleId=await db.Roles.Where(x=>x.Code==role).Select(x=>x.Id).SingleAsync()});await db.SaveChangesAsync();
        var c=f.Client();var login=await f.Login(c,email);c.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);return(c,user.Id);
    }
    private async Task<Data> Setup()
    {
        var(c,actor)=await Login();using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<AppDbContext>();var tag=Guid.NewGuid().ToString("N").ToUpperInvariant();
        var patient=new PatientProfile{User=new User{FirstName="PacienteCaja",LastName=tag,DocumentType="CI", DocumentCountryCode="BO", DocumentNumber="CAJA-"+tag,BirthDate=new(1990,1,1),Phone="70012345",Email=tag.ToLowerInvariant()+"@example.test",CanLogin=false},BloodType="UNKNOWN",EmergencyContactName="Test",EmergencyContactPhone="70000000"};db.Add(patient);
        db.UserRoles.Add(new(){User=patient.User,RoleId=await db.Roles.Where(x=>x.Code=="PATIENT").Select(x=>x.Id).SingleAsync()});
        var doctor=new DoctorProfile{User=new User{FirstName="Doctor",LastName="Caja",CanLogin=false},ProfessionalLicense=tag};db.Add(doctor);db.UserRoles.Add(new(){User=doctor.User,RoleId=await db.Roles.Where(x=>x.Code=="DOCTOR").Select(x=>x.Id).SingleAsync()});
        var ep=new TreatmentEpisode{PatientProfileId=patient.Id,PrimaryDoctorProfileId=doctor.Id,Title="Tratamiento financiero"};db.Add(ep);
        db.Add(new TreatmentAnamnesis{TreatmentEpisodeId=ep.Id,ChiefComplaint="PRIVATE ANAMNESIS",PainSince="PRIVATE",RecordedByUserId=doctor.UserId});
        var plan=new TreatmentPlan{TreatmentEpisodeId=ep.Id,Title="Plan",Status="ACCEPTED",AcceptedAt=DateTime.UtcNow};db.Add(plan);db.Add(new TreatmentPlanItem{TreatmentPlanId=plan.Id,ProcedureName="Procedimiento",Description="PRIVATE CLINICAL DETAIL",UnitPrice=250m,Quantity=4,Status="ACCEPTED"});db.Add(new TreatmentFinancialAccount{TreatmentPlanId=plan.Id,AgreedTotal=1000m});await db.SaveChangesAsync();return new(c,actor,patient.Id,plan.Id,patient.User.DocumentNumber!,patient.User.Email!);
    }
    private static string Url(Data d)=>$"/api/cashier/treatments/{d.Plan}";
    private static CashierPaymentRequest Payment(decimal amount=300m)=>new(Guid.NewGuid(),amount,"CASH","REF","Nota financiera");
    private static async Task<CashierDetail> Detail(Data d)=>(await d.Client.GetFromJsonAsync<CashierDetail>(Url(d)))!;
    private static async Task Pay(Data d,CashierPaymentRequest? p=null){var r=await d.Client.PostAsJsonAsync(Url(d)+"/payments",p??Payment());Assert.True(r.IsSuccessStatusCode,await r.Content.ReadAsStringAsync());}
    [Fact] public async Task CashierCanAccessFinancialTreatment(){var d=await Setup();var x=await Detail(d);Assert.Equal(1000m,x.Treatment.AgreedTotal);Assert.Equal(0m,x.Treatment.PaidTotal);Assert.Equal(1000m,x.Treatment.PendingBalance);Assert.Equal("PENDING",x.Treatment.PaymentStatus);Assert.Equal(1000m,Assert.Single(x.Procedures).Subtotal);}
    [Theory][InlineData("DOCTOR")][InlineData("PATIENT")][InlineData("NONE")]
    public async Task WithoutFinancialPermissionForbidden(string role){var(c,_)=await Login(role);Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/cashier/patients/search?q=test")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync($"/api/cashier/treatments/{Guid.NewGuid()}/payments",Payment())).StatusCode);}
    [Fact] public async Task UnauthenticatedUnauthorized(){using var c=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/cashier/patients/search?q=test")).StatusCode);}
    [Theory][InlineData("documentNumber")][InlineData("name")][InlineData("lastName")][InlineData("phone")][InlineData("email")]
    public async Task PartialRemoteSearch(string field){var d=await Setup();var q=field switch{"documentNumber"=>d.DocumentNumber[5..],"name"=>"PacienteCaja","lastName"=>d.DocumentNumber[5..],"phone"=>"001234","email"=>d.Email[..15],_=>""};var list=(await d.Client.GetFromJsonAsync<CashierPatient[]>("/api/cashier/patients/search?q="+Uri.EscapeDataString(q)))!;Assert.True(list.Length<=10);if(field is "documentNumber" or "email" or "lastName")Assert.Contains(list,x=>x.PatientId==d.Patient);else Assert.NotEmpty(list);}
    [Theory][InlineData("")][InlineData("a")][InlineData("ab&limit=11")][InlineData("ab&limit=0")]
    public async Task SearchValidation(string query){var(c,_)=await Login();Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/api/cashier/patients/search?q="+query)).StatusCode);}
    [Fact] public async Task OnlyPlansWithAccountsReturned(){var d=await Setup();using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<AppDbContext>();var first=await db.Set<TreatmentPlan>().SingleAsync(x=>x.Id==d.Plan);var ep=await db.Set<TreatmentEpisode>().SingleAsync(x=>x.Id==first.TreatmentEpisodeId);var other=new TreatmentEpisode{PatientProfileId=ep.PatientProfileId,PrimaryDoctorProfileId=ep.PrimaryDoctorProfileId,Title="Sin cuenta"};db.Add(other);db.Add(new TreatmentPlan{TreatmentEpisodeId=other.Id,Title="Borrador"});await db.SaveChangesAsync();Assert.Single((await d.Client.GetFromJsonAsync<CashierTreatment[]>($"/api/cashier/patients/{d.Patient}/treatments"))!);}
    [Fact] public async Task ValidPaymentActorAuditAndBalance(){var d=await Setup();await Pay(d);var x=await Detail(d);Assert.Equal(300m,x.Treatment.PaidTotal);Assert.Equal(700m,x.Treatment.PendingBalance);var payment=Assert.Single(x.Payments);Assert.Equal("REF",payment.Reference);Assert.Equal("PAID",payment.Status);using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal(d.Actor,(await db.Set<TreatmentPayment>().SingleAsync(p=>p.Id==payment.Id)).RecordedByUserId);var audit=await db.AuditLogs.SingleAsync(a=>a.Action=="payment_recorded"&&a.EntityId==payment.Id);Assert.Equal(d.Actor,audit.UserId);var metadata=JsonDocument.Parse(audit.MetadataJson!).RootElement;Assert.Equal(d.Plan,metadata.GetProperty("TreatmentPlanId").GetGuid());Assert.Equal(300m,metadata.GetProperty("Amount").GetDecimal());Assert.DoesNotContain("Nota financiera",audit.MetadataJson);}
    [Theory][InlineData(0)][InlineData(-1)][InlineData(0.001)][InlineData(10000000000)]
    public async Task InvalidAmountRejected(decimal amount){var d=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await d.Client.PostAsJsonAsync(Url(d)+"/payments",Payment(amount))).StatusCode);Assert.Empty((await Detail(d)).Payments);}
    [Theory][InlineData("CASH")][InlineData("TRANSFER")][InlineData("CARD")][InlineData("QR")][InlineData("OTHER")]
    public async Task ControlledMethodsAccepted(string method){var d=await Setup();await Pay(d,Payment(1m) with{PaymentMethod=method});Assert.Equal(method,Assert.Single((await Detail(d)).Payments).PaymentMethod);}
    [Theory][InlineData("")][InlineData("BITCOIN")]
    public async Task InvalidMethodRejected(string method){var d=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await d.Client.PostAsJsonAsync(Url(d)+"/payments",Payment() with{PaymentMethod=method})).StatusCode);}
    [Fact] public async Task ForgedRecorderRejected(){var d=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await d.Client.PostAsJsonAsync(Url(d)+"/payments",new{amount=1,paymentMethod="CASH",idempotencyKey=Guid.NewGuid(),recordedByUserId=Guid.NewGuid()})).StatusCode);Assert.Empty((await Detail(d)).Payments);}
    [Fact] public async Task FullPaymentAndSubsequentPaymentRejected(){var d=await Setup();await Pay(d,Payment(1000m));var t=(await Detail(d)).Treatment;Assert.Equal(0m,t.PendingBalance);Assert.Equal("PAID",t.PaymentStatus);Assert.Equal(HttpStatusCode.Conflict,(await d.Client.PostAsJsonAsync(Url(d)+"/payments",Payment(1m))).StatusCode);}
    [Fact] public async Task OverpaymentRejected(){var d=await Setup();await Pay(d);Assert.Equal(HttpStatusCode.Conflict,(await d.Client.PostAsJsonAsync(Url(d)+"/payments",Payment(701m))).StatusCode);Assert.Equal(700m,(await Detail(d)).Treatment.PendingBalance);}
    [Fact] public async Task TwoCashiersCannotOverpay(){var d=await Setup();var(other,_)=await Login();var requests=await Task.WhenAll(d.Client.PostAsJsonAsync(Url(d)+"/payments",Payment(1000m)),other.PostAsJsonAsync(Url(d)+"/payments",Payment(1000m)));Assert.Single(requests,x=>x.StatusCode==HttpStatusCode.NoContent);Assert.Single(requests,x=>x.StatusCode==HttpStatusCode.Conflict);Assert.Single((await Detail(d)).Payments);}
    [Fact] public async Task DuplicateRequestIdempotent(){var d=await Setup();var p=Payment();foreach(var r in await Task.WhenAll(Enumerable.Range(0,3).Select(_=>d.Client.PostAsJsonAsync(Url(d)+"/payments",p))))Assert.Equal(HttpStatusCode.NoContent,r.StatusCode);Assert.Single((await Detail(d)).Payments);Assert.Equal(HttpStatusCode.Conflict,(await d.Client.PostAsJsonAsync(Url(d)+"/payments",p with{Amount=1m})).StatusCode);}
    [Fact] public async Task PatientHistoryContainsOnlyTheirPaymentsNewestFirst(){var d=await Setup();var other=await Setup();await Pay(d);await Pay(d,Payment(50m));await Pay(other);var rows=(await d.Client.GetFromJsonAsync<CashierPayment[]>($"/api/cashier/patients/{d.Patient}/payments"))!;Assert.Equal(2,rows.Length);Assert.Equal(50m,rows[0].Amount);Assert.All(rows,x=>Assert.Equal(d.Plan,x.TreatmentPlanId));}
    [Fact] public async Task GlobalHistorySpansMultipleTreatmentsWithoutMixingTheirBalances()
    {
        var d=await Setup();Guid secondPlan;
        using(var s=f.Services.CreateScope()){
            var db=s.ServiceProvider.GetRequiredService<AppDbContext>();var first=await db.Set<TreatmentPlan>().SingleAsync(x=>x.Id==d.Plan);var original=await db.Set<TreatmentEpisode>().SingleAsync(x=>x.Id==first.TreatmentEpisodeId);
            var episode=new TreatmentEpisode{PatientProfileId=d.Patient,PrimaryDoctorProfileId=original.PrimaryDoctorProfileId,Title="Segundo tratamiento"};db.Add(episode);
            var plan=new TreatmentPlan{TreatmentEpisodeId=episode.Id,Title="Segundo plan",Status="ACCEPTED"};db.Add(plan);db.Add(new TreatmentFinancialAccount{TreatmentPlanId=plan.Id,AgreedTotal=500m});await db.SaveChangesAsync();secondPlan=plan.Id;
        }
        await Pay(d,Payment(100m));await Pay(d with{Plan=secondPlan},Payment(50m));
        var history=(await d.Client.GetFromJsonAsync<CashierPayment[]>($"/api/cashier/patients/{d.Patient}/payments"))!;
        Assert.Equal(2,history.Length);Assert.Equal(secondPlan,history[0].TreatmentPlanId);Assert.Equal(d.Plan,history[1].TreatmentPlanId);
        Assert.Equal(900m,(await Detail(d)).Treatment.PendingBalance);Assert.Equal(450m,(await Detail(d with{Plan=secondPlan})).Treatment.PendingBalance);
    }
    [Fact] public async Task NoClinicalInformationAndNoWriteAccess(){var d=await Setup();var json=await d.Client.GetStringAsync(Url(d));foreach(var word in new[]{"PRIVATE","anamnesis","soap","allergies","medications","conditions","description","toothNumber"})Assert.DoesNotContain(word,json,StringComparison.OrdinalIgnoreCase);foreach(var path in new[]{"/api/admin/appointments","/api/admin/users",$"/api/doctor/appointments/{Guid.NewGuid()}/clinical"})Assert.Equal(HttpStatusCode.Forbidden,(await d.Client.GetAsync(path)).StatusCode);Assert.Equal(HttpStatusCode.MethodNotAllowed,(await d.Client.DeleteAsync(Url(d))).StatusCode);}
    [Fact] public async Task ReadPermissionAloneDoesNotAllowPayment(){var d=await Setup();var(c,actor)=await Login("NONE");using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<AppDbContext>();var role=new Role{Code="READ_"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Name=Guid.NewGuid().ToString()};db.Add(role);db.RolePermissions.Add(new(){RoleId=role.Id,PermissionId=await db.Permissions.Where(x=>x.Code=="payments.view").Select(x=>x.Id).SingleAsync()});db.UserRoles.Add(new(){RoleId=role.Id,UserId=actor});await db.SaveChangesAsync();Assert.Equal(HttpStatusCode.OK,(await c.GetAsync(Url(d))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsJsonAsync(Url(d)+"/payments",Payment())).StatusCode);}
    [Fact] public async Task MissingResourcesReturn404(){var(c,_)=await Login();foreach(var path in new[]{$"treatments/{Guid.NewGuid()}",$"patients/{Guid.NewGuid()}/treatments",$"patients/{Guid.NewGuid()}/payments"})Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync("/api/cashier/"+path)).StatusCode);}
}
