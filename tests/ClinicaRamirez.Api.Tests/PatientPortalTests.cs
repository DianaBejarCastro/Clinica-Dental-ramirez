using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClinicaRamirez.Api.Tests;
public sealed class PatientPortalTests(IdentityFixture f):IClassFixture<IdentityFixture>
{
 private async Task<(HttpClient Client,Guid Id)> Setup()
 {
  var email=await f.CreateUser();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var user=await db.Users.SingleAsync(x=>x.Email==email);
  user.DocumentType="CI";user.DocumentCountryCode="BO";user.DocumentNumber=Guid.NewGuid().ToString("N").ToUpperInvariant();user.BirthDate=new(1990,1,1);user.Phone="70000000";
  var patient=new PatientProfile{User=user,EmergencyContactName="Contact",EmergencyContactPhone="70000000",BloodType="UNKNOWN"};db.Add(patient);
  db.UserRoles.Add(new(){UserId=user.Id,RoleId=await db.Roles.Where(x=>x.Code=="PATIENT").Select(x=>x.Id).SingleAsync()});
  var doctor=new DoctorProfile{User=new User{FirstName="Doctor",LastName="Portal",CanLogin=false},ProfessionalLicense=Guid.NewGuid().ToString("N").ToUpperInvariant()};db.Add(doctor);
  var center=new Center{ClinicId=Clinic.SingletonId,Name="Centro portal",Code="C"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Address="Dirección",Country="Bolivia"};db.Add(center);
  var specialty=await db.Specialties.Select(x=>x.Id).FirstAsync();
  foreach(var (status,day) in new[]{("SCHEDULED",DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2))),("COMPLETED",new DateOnly(2020,1,1)),("CANCELLED",DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)))})
   db.Appointments.Add(new(){PatientId=patient.Id,DoctorId=doctor.Id,CenterId=center.Id,SpecialtyId=specialty,AppointmentDate=day,StartTime=new(9,0),EndTime=new(9,30),Status=status,Reason="Consulta"});
  var ep=new TreatmentEpisode{PatientProfileId=patient.Id,PrimaryDoctorProfileId=doctor.Id,Title="Tratamiento propio"};db.Add(ep);var plan=new TreatmentPlan{TreatmentEpisodeId=ep.Id,Title="Plan",Status="ACCEPTED"};db.Add(plan);
  var account=new TreatmentFinancialAccount{TreatmentPlanId=plan.Id,AgreedTotal=1000m};db.Add(account);
  db.Add(new TreatmentPayment{FinancialAccountId=account.Id,Amount=200m,PaymentMethod="CASH",PaidAt=DateTime.UtcNow.AddDays(-2),IdempotencyKey=Guid.NewGuid(),RecordedByUserId=user.Id});
  db.Add(new TreatmentPayment{FinancialAccountId=account.Id,Amount=100m,PaymentMethod="QR",PaidAt=DateTime.UtcNow.AddDays(-1),IdempotencyKey=Guid.NewGuid(),RecordedByUserId=user.Id});
  await db.SaveChangesAsync();var client=f.Client();var login=await f.Login(client,email);client.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);return(client,patient.Id);
 }
 [Fact] public async Task DashboardUsesOwnIdentityAndSharedFinancialBalances()
 {
  var me=await Setup();await Setup();var result=(await me.Client.GetFromJsonAsync<PatientDashboardResponse>("/api/patient/dashboard"))!;
  Assert.Equal(1000m,result.Finance.TotalEstimated);Assert.Equal(300m,result.Finance.TotalPaid);Assert.Equal(700m,result.Finance.PendingBalance);Assert.Equal(1,result.Finance.PendingTreatments);
  Assert.Equal("SCHEDULED",result.NextAppointment!.Status);Assert.NotNull(result.NextAppointment.CreatedAt);Assert.Equal("Consulta",result.NextAppointment.Reason);Assert.Equal(2,result.RecentPayments.Length);Assert.Equal(100m,result.RecentPayments[0].Amount);
 }
 [Theory][InlineData("dashboard")][InlineData("payments")][InlineData("appointments/history")]
 public async Task ForgedPatientIdRejected(string route){var me=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await me.Client.GetAsync($"/api/patient/{route}?patientId={Guid.NewGuid()}")).StatusCode);}
 [Fact] public async Task PaymentAndTreatmentPagesStayScopedAndOrdered()
 {
  var me=await Setup();await Setup();var first=(await me.Client.GetFromJsonAsync<PatientPayments>("/api/patient/payments?pageSize=1"))!;
  Assert.Equal(2,first.Payments.Total);Assert.Single(first.Payments.Items);Assert.Equal(100m,first.Payments.Items[0].Amount);Assert.Single(first.Treatments.Items);Assert.Equal(700m,first.Treatments.Items[0].Balance);
  var second=(await me.Client.GetFromJsonAsync<PatientPayments>("/api/patient/payments?pageSize=1&page=2"))!;Assert.Equal(200m,Assert.Single(second.Payments.Items).Amount);
 }
 [Theory][InlineData("upcoming",1)][InlineData("history",2)][InlineData("completed",1)][InlineData("cancelled",1)][InlineData("all",3)]
 public async Task AppointmentFiltersUseActualStatuses(string filter,int count){var me=await Setup();await Setup();var result=(await me.Client.GetFromJsonAsync<PatientPage<PatientAppointmentResponse>>($"/api/patient/appointments/history?filter={filter}&pageSize=1"))!;Assert.Equal(count,result.Total);Assert.Single(result.Items);}
 [Theory][InlineData("payments?pageSize=21")][InlineData("payments?page=0")][InlineData("appointments/history?filter=invalid")][InlineData("appointments/history?page=-1")]
 public async Task InvalidPagingOrFilterRejected(string route){var me=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await me.Client.GetAsync("/api/patient/"+route)).StatusCode);}
 [Fact] public async Task PortalRequiresPatientAndRejectsWrites()
 {
  using var anon=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await anon.GetAsync("/api/patient/dashboard")).StatusCode);
  var login=await f.Login(anon,f.Email);anon.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);Assert.Equal(HttpStatusCode.Forbidden,(await anon.GetAsync("/api/patient/payments")).StatusCode);
  var me=await Setup();Assert.Equal(HttpStatusCode.MethodNotAllowed,(await me.Client.PostAsJsonAsync("/api/patient/payments",new{amount=1})).StatusCode);
  using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();(await db.PatientProfiles.SingleAsync(x=>x.Id==me.Id)).IsActive=false;await db.SaveChangesAsync();Assert.Equal(HttpStatusCode.Forbidden,(await me.Client.GetAsync("/api/patient/dashboard")).StatusCode);
 }
}
