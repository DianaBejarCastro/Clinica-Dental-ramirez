using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClinicaRamirez.Api.Tests;
public sealed class PatientBookingTests(IdentityFixture f):IClassFixture<IdentityFixture>
{
 const string Url="/api/patient/appointments";
 record Data(HttpClient Client,Guid Patient,Guid Doctor,Guid Center,Guid Specialty,Guid Featured,DateOnly Date);
 async Task<(HttpClient Client,Guid Id)> Patient()
 {
  var email=await f.CreateUser();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var user=await db.Users.SingleAsync(x=>x.Email==email);
  user.DocumentType="CI";user.DocumentCountryCode="BO";user.DocumentNumber="P"+Guid.NewGuid().ToString("N").ToUpperInvariant();user.BirthDate=new(1990,1,1);user.Phone="70000000";
  var p=new PatientProfile{User=user,EmergencyContactName="Contact",EmergencyContactPhone="70000000",BloodType="UNKNOWN"};db.Add(p);db.UserRoles.Add(new(){UserId=user.Id,RoleId=await db.Roles.Where(x=>x.Code=="PATIENT").Select(x=>x.Id).SingleAsync()});await db.SaveChangesAsync();var c=f.Client();var login=await f.Login(c,email);c.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);return(c,p.Id);
 }
 async Task<Data> Setup()
 {
  var patient=await Patient();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
  var doctor=new DoctorProfile{User=new User{FirstName="Doctor",LastName="Booking",CanLogin=false},ProfessionalLicense="D"+Guid.NewGuid().ToString("N").ToUpperInvariant()};
  var center=new Center{ClinicId=Clinic.SingletonId,Name="Booking",Code="C"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Address="Address"};var specialty=new Specialty{Code="S"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Name="Especialidad "+Guid.NewGuid().ToString("N")};db.AddRange(doctor,center,specialty);
  db.DoctorCenters.Add(new(){DoctorId=doctor.Id,CenterId=center.Id});db.DoctorSpecialties.Add(new(){DoctorId=doctor.Id,SpecialtyId=specialty.Id});
  db.DoctorSchedules.Add(new(){DoctorId=doctor.Id,CenterId=center.Id,DayOfWeek=1,StartTime=new(8,0),EndTime=new(12,0),HasBreak=true,BreakStart=new(10,0),BreakEnd=new(10,30)});
  var feature=new ClinicFeaturedDoctor{ClinicId=Clinic.SingletonId,DoctorProfileId=doctor.Id};db.Add(feature);await db.SaveChangesAsync();var date=DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));while(date.DayOfWeek!=DayOfWeek.Monday)date=date.AddDays(1);
  return new(patient.Client,patient.Id,doctor.Id,center.Id,specialty.Id,feature.Id,date);
 }
 static PatientBookingRequest Body(Data d)=>new(d.Featured,d.Date,new(8,0),d.Center,"Revisión");
 static string Availability(Data d)=>$"{Url}/availability?featuredDoctorId={d.Featured}&year={d.Date.Year}&month={d.Date.Month}";
 async Task AddAppointment(Data d,string status,DateOnly? date=null,Guid? patient=null){using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();db.Appointments.Add(new(){PatientId=patient??d.Patient,DoctorId=d.Doctor,CenterId=d.Center,SpecialtyId=d.Specialty,AppointmentDate=date??d.Date,StartTime=new(8,0),EndTime=new(8,45),Status=status});await db.SaveChangesAsync();}
 [Fact] public async Task MonthUsesSchedulesBreaksAndCompleteIntervals(){var d=await Setup();var a=(await d.Client.GetFromJsonAsync<PatientBookingAvailability>(Availability(d)))!;Assert.Null(a.ExistingAppointment);Assert.NotEmpty(a.Slots);Assert.All(a.Slots,s=>{Assert.Equal(DayOfWeek.Monday,s.Date.DayOfWeek);Assert.Equal(TimeSpan.FromMinutes(45),s.EndTime-s.StartTime);Assert.True(s.EndTime<=new TimeOnly(12,0));Assert.True(s.EndTime<=new TimeOnly(10,0)||s.StartTime>=new TimeOnly(10,30));Assert.Equal(d.Center,s.CenterId);});Assert.Contains(a.Slots,s=>s.Date==d.Date&&s.StartTime==new TimeOnly(8,0));}
 [Fact] public async Task BookingPersistsOwnPatientExactly45MinutesAndAppearsInPortal(){var d=await Setup();var response=await d.Client.PostAsJsonAsync(Url,Body(d));Assert.Equal(HttpStatusCode.Created,response.StatusCode);var a=(await response.Content.ReadFromJsonAsync<PatientAppointmentResponse>())!;Assert.Equal(new TimeOnly(8,45),a.EndTime);using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var stored=await db.Appointments.SingleAsync(x=>x.Id==a.Id);Assert.Equal(d.Patient,stored.PatientId);Assert.Equal(d.Center,stored.CenterId);Assert.Equal(d.Specialty,stored.SpecialtyId);Assert.NotNull(stored.CreatedByUserId);Assert.True(await db.AuditLogs.AnyAsync(x=>x.Action=="appointment_created"&&x.EntityId==a.Id));var dash=(await d.Client.GetFromJsonAsync<PatientDashboardResponse>("/api/patient/dashboard"))!;Assert.Equal(a.Id,dash.NextAppointment!.Id);}
 [Theory][InlineData("patientId")][InlineData("durationMinutes")][InlineData("endTime")][InlineData("specialtyId")]
 public async Task ForgedFieldsRejected(string field){var d=await Setup();var json=JsonSerializer.SerializeToNode(Body(d),new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();json[field]="forged";Assert.Equal(HttpStatusCode.BadRequest,(await d.Client.PostAsJsonAsync(Url,json)).StatusCode);}
 [Theory][InlineData(7,0)][InlineData(9,30)][InlineData(10,0)][InlineData(11,45)][InlineData(8,17)]
 public async Task InvalidTimesRejected(int hour,int minute){var d=await Setup();Assert.Equal(HttpStatusCode.Conflict,(await d.Client.PostAsJsonAsync(Url,Body(d) with{StartTime=new(hour,minute)})).StatusCode);}
 [Fact] public async Task PastAndNonWorkingDatesRejected(){var d=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await d.Client.PostAsJsonAsync(Url,Body(d) with{Date=new(2020,1,1)})).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await d.Client.PostAsJsonAsync(Url,Body(d) with{Date=d.Date.AddDays(1)})).StatusCode);}
 [Fact] public async Task InventedCenterAndDoctorRejected(){var d=await Setup();Assert.Equal(HttpStatusCode.NotFound,(await d.Client.PostAsJsonAsync(Url,Body(d) with{FeaturedDoctorId=Guid.NewGuid()})).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await d.Client.PostAsJsonAsync(Url,Body(d) with{CenterId=Guid.NewGuid()})).StatusCode);}
 [Fact] public async Task InactiveDoctorRejected(){var d=await Setup();using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();(await db.DoctorProfiles.SingleAsync(x=>x.Id==d.Doctor)).IsActive=false;await db.SaveChangesAsync();Assert.Equal(HttpStatusCode.NotFound,(await d.Client.PostAsJsonAsync(Url,Body(d))).StatusCode);}
 [Theory][InlineData("SCHEDULED")][InlineData("CONFIRMED")][InlineData("IN_PROGRESS")]
 public async Task ExistingFutureAppointmentBlocksAnother(string status){var d=await Setup();await AddAppointment(d,status);var a=(await d.Client.GetFromJsonAsync<PatientBookingAvailability>(Availability(d)))!;Assert.NotNull(a.ExistingAppointment);Assert.Empty(a.Slots);Assert.Equal(HttpStatusCode.Conflict,(await d.Client.PostAsJsonAsync(Url,Body(d) with{StartTime=new(8,45)})).StatusCode);}
 [Theory][InlineData("CANCELLED")][InlineData("NO_SHOW")]
 public async Task NonBlockingStatusesAllowSlot(string status){var d=await Setup();await AddAppointment(d,status);Assert.Equal(HttpStatusCode.Created,(await d.Client.PostAsJsonAsync(Url,Body(d))).StatusCode);}
 [Fact] public async Task PastAppointmentAllowsNewWithoutChangingHistory(){var d=await Setup();await AddAppointment(d,"SCHEDULED",new(2020,1,1));Assert.Equal(HttpStatusCode.Created,(await d.Client.PostAsJsonAsync(Url,Body(d))).StatusCode);using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();Assert.Equal("SCHEDULED",(await db.Appointments.SingleAsync(x=>x.PatientId==d.Patient&&x.AppointmentDate==new DateOnly(2020,1,1))).Status);}
 [Fact] public async Task OccupiedByAnotherPatientUnavailable(){var d=await Setup();var other=await Patient();await AddAppointment(d,"CONFIRMED",patient:other.Id);var a=(await d.Client.GetFromJsonAsync<PatientBookingAvailability>(Availability(d)))!;Assert.DoesNotContain(a.Slots,x=>x.Date==d.Date&&x.StartTime==new TimeOnly(8,0));Assert.Equal(HttpStatusCode.Conflict,(await d.Client.PostAsJsonAsync(Url,Body(d))).StatusCode);}
 [Fact] public async Task ConcurrentPatientsCannotDoubleBook(){var d=await Setup();var other=await Patient();var results=await Task.WhenAll(d.Client.PostAsJsonAsync(Url,Body(d)),other.Client.PostAsJsonAsync(Url,Body(d)));Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Created);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);}
 [Fact] public async Task SamePatientConcurrentDifferentSlotsOnlyOne(){var d=await Setup();var results=await Task.WhenAll(d.Client.PostAsJsonAsync(Url,Body(d)),d.Client.PostAsJsonAsync(Url,Body(d) with{StartTime=new(8,45)}));Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Created);Assert.Single(results,x=>x.StatusCode==HttpStatusCode.Conflict);}
 [Fact] public async Task AnonymousAndAdminCannotBook(){var d=await Setup();using var client=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync(Url,Body(d))).StatusCode);var login=await f.Login(client,f.Email);client.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(Url,Body(d))).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(Availability(d))).StatusCode);}

 [Fact] public async Task PastTimeTodayIsNeitherOfferedNorAccepted()
 {
  var d=await Setup();var now=TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"America/La_Paz");var today=DateOnly.FromDateTime(now);
  using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var schedule=await db.DoctorSchedules.SingleAsync(x=>x.DoctorId==d.Doctor);schedule.DayOfWeek=((int)today.DayOfWeek+6)%7+1;schedule.StartTime=new(0,0);schedule.EndTime=new(23,59);schedule.HasBreak=false;schedule.BreakStart=null;schedule.BreakEnd=null;await db.SaveChangesAsync();}
  var a=(await d.Client.GetFromJsonAsync<PatientBookingAvailability>($"{Url}/availability?featuredDoctorId={d.Featured}&year={today.Year}&month={today.Month}"))!;
  Assert.All(a.Slots.Where(x=>x.Date==today),x=>Assert.True(x.StartTime>TimeOnly.FromDateTime(now)));
  Assert.Equal(HttpStatusCode.Conflict,(await d.Client.PostAsJsonAsync(Url,Body(d) with{Date=today,StartTime=new(0,0)})).StatusCode);
 }
 [Fact] public async Task DoctorRoleCannotUsePatientEndpoint()
 {
  var d=await Setup();var email=await f.CreateUser();using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var user=await db.Users.SingleAsync(x=>x.Email==email);db.UserRoles.Add(new(){UserId=user.Id,RoleId=await db.Roles.Where(x=>x.Code=="DOCTOR").Select(x=>x.Id).SingleAsync()});await db.SaveChangesAsync();}
  using var client=f.Client();var login=await f.Login(client,email);client.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsJsonAsync(Url,Body(d))).StatusCode);
 }
 [Theory][InlineData("user")][InlineData("publication")][InlineData("center")]
 public async Task DeactivationBeforeConfirmIsRespected(string target)
 {
  var d=await Setup();await d.Client.GetAsync(Availability(d));using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();if(target=="user")(await db.DoctorProfiles.Include(x=>x.User).SingleAsync(x=>x.Id==d.Doctor)).User.IsActive=false;else if(target=="publication")(await db.ClinicFeaturedDoctors.SingleAsync(x=>x.Id==d.Featured)).IsActive=false;else (await db.Centers.SingleAsync(x=>x.Id==d.Center)).IsActive=false;await db.SaveChangesAsync();}
  Assert.Equal(HttpStatusCode.NotFound,(await d.Client.PostAsJsonAsync(Url,Body(d))).StatusCode);
 }
}
