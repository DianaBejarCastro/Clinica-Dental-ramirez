using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicaRamirez.Api.Domain.Appointments;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Domain.DentalCharting;
using ClinicaRamirez.Api.DTOs.DentalCharting;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;
namespace ClinicaRamirez.Api.Tests;
public sealed class DentalChartingTests(IdentityFixture f) : IClassFixture<IdentityFixture>
{
    private sealed record Care(HttpClient Client, Appointment Appointment, Guid User);
    private async Task<Care> Setup(bool finance=false)
    {
        var email=await f.CreateUser();using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<AppDbContext>();var user=await db.Users.SingleAsync(x=>x.Email==email);
        var d=new DoctorProfile{UserId=user.Id,ProfessionalLicense=Guid.NewGuid().ToString("N").ToUpperInvariant()};db.Add(d);
        foreach(var role in finance?new[]{"DOCTOR","ADMINISTRATOR"}:new[]{"DOCTOR"})db.UserRoles.Add(new(){UserId=user.Id,RoleId=await db.Roles.Where(x=>x.Code==role).Select(x=>x.Id).SingleAsync()});
        var p=new PatientProfile{User=new User{FirstName="Paciente",LastName="ClÃƒÂ­nico",Ci=Guid.NewGuid().ToString("N").ToUpperInvariant(),BirthDate=new(1990,1,1),Phone="70000000",CanLogin=false},BloodType="UNKNOWN",EmergencyContactName="Prueba",EmergencyContactPhone="70000000"};db.Add(p);db.UserRoles.Add(new(){User=p.User,RoleId=await db.Roles.Where(x=>x.Code=="PATIENT").Select(x=>x.Id).SingleAsync()});
        var center=new Center{ClinicId=Clinic.SingletonId,Code="CL_"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Name="Prueba",Address="Prueba"};db.Add(center);
        var a=new Appointment{DoctorId=d.Id,PatientId=p.Id,CenterId=center.Id,SpecialtyId=await db.Specialties.Select(x=>x.Id).FirstAsync(),AppointmentDate=new(2026,10,14),StartTime=new(9,0),EndTime=new(9,30)};db.Add(a);await db.SaveChangesAsync();var client=f.Client();var login=await f.Login(client,email);client.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);return new(client,a,user.Id);
    }
    private static readonly DateTimeOffset February = new(2026,2,20,12,0,0,TimeSpan.Zero), September = new(2026,9,20,12,0,0,TimeSpan.Zero);
    private static string PathFor(Care c,string kind)=>$"/api/doctor/patients/{c.Appointment.PatientId}/{kind}";
    private static string ExamPath(Guid id,string kind="odontograms")=>$"/api/doctor/{kind}/{id}";
    private static async Task<T> Read<T>(Task<HttpResponseMessage> request) {
        var r=await request;Assert.True(r.IsSuccessStatusCode,$"{r.StatusCode}: {await r.Content.ReadAsStringAsync()}");return (await r.Content.ReadFromJsonAsync<T>())!;
    }
    private static async Task Ok(Task<HttpResponseMessage> request) {var r=await request;Assert.True(r.IsSuccessStatusCode,$"{r.StatusCode}: {await r.Content.ReadAsStringAsync()}");}
    private Task<OdontogramDetail> Odo(Care c,string mode="PERMANENT",Guid? encounter=null)=>Read<OdontogramDetail>(c.Client.PostAsJsonAsync(PathFor(c,"odontograms"),new ExamCreateRequest(mode,February,encounter,null)));
    private Task<PeriodontalDetail> Perio(Care c,string mode="PERMANENT",Guid? encounter=null)=>Read<PeriodontalDetail>(c.Client.PostAsJsonAsync(PathFor(c,"periodontal-exams"),new ExamCreateRequest(mode,February,encounter,null)));
    private static OdontogramUpdateRequest Findings(params FindingInput[] findings)=>new(0,"PERMANENT","Private notes",findings);
    private static PeriodontalUpdateRequest Measures(params MeasurementInput[] measurements)=>new(0,"PERMANENT",null,measurements,[],[]);
    private async Task<Guid> Encounter(Care c) {
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var episode=new TreatmentEpisode{PatientProfileId=c.Appointment.PatientId,PrimaryDoctorProfileId=c.Appointment.DoctorId,Title="Test episode"};
        var review=new PatientMedicalHistoryReview{PatientProfileId=c.Appointment.PatientId,ReviewedByUserId=c.User};db.Add(review);
        var e=new ClinicalEncounter{MedicalHistoryReviewId=review.Id,AppointmentId=c.Appointment.Id,TreatmentEpisodeId=episode.Id,PatientProfileId=c.Appointment.PatientId,DoctorProfileId=c.Appointment.DoctorId};db.Add(episode);db.Add(e);await db.SaveChangesAsync();return e.Id;
    }
    [Fact] public async Task CatalogsContain52UniqueTeethAndControlledCodes() {
        var c=await Setup();var teeth=(await c.Client.GetFromJsonAsync<DentalToothDto[]>("/api/doctor/dental-catalogs/teeth"))!;
        Assert.Equal(52,teeth.Length);Assert.Equal(32,teeth.Count(x=>x.DentitionClass=="PERMANENT"));Assert.Equal(20,teeth.Count(x=>x.DentitionClass=="PRIMARY"));Assert.Equal(52,teeth.Select(x=>x.FdiCode).Distinct().Count());
        var surfaces=(await c.Client.GetFromJsonAsync<DentalSurfaceDto[]>("/api/doctor/dental-catalogs/surfaces"))!;Assert.Equal(5,surfaces.Length);Assert.Equal(5,surfaces.Select(x=>x.Code).Distinct().Count());
        var findings=(await c.Client.GetFromJsonAsync<FindingTypeDto[]>("/api/doctor/dental-catalogs/finding-types"))!;Assert.Equal(33,findings.Length);Assert.Equal(33,findings.Select(x=>x.Code).Distinct().Count());
        var sites=(await c.Client.GetFromJsonAsync<PeriodontalSiteDto[]>("/api/doctor/dental-catalogs/periodontal-sites"))!;Assert.Equal(new[]{"MB","B","DB","ML","L","DL"},sites.Select(x=>x.Code));
    }
    [Theory][InlineData("PERMANENT",46)][InlineData("PRIMARY",55)][InlineData("MIXED",46)][InlineData("MIXED",55)]
    public async Task OwnPatientAndDentition(string mode,int tooth) {
        var c=await Setup();var encounter=await Encounter(c);var o=await Odo(c,mode,encounter);
        Assert.Equal(c.Appointment.PatientId,o.Exam.PatientProfileId);Assert.Equal(encounter,o.Exam.ClinicalEncounterId);
        await Ok(c.Client.PutAsJsonAsync(ExamPath(o.Exam.Id),new OdontogramUpdateRequest(0,mode,null,[new(tooth,3,[2,4],null)])));
        var got=await c.Client.GetFromJsonAsync<OdontogramDetail>(ExamPath(o.Exam.Id));Assert.Equal(new[]{2,4},Assert.Single(got!.Findings).SurfaceIds);
    }
    [Fact] public async Task WholeToothMissingNeedsNoArtificialSurfaces() {
        var c=await Setup();var e=await Odo(c);await Ok(c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id),Findings(new FindingInput(18,11,[],null))));
        Assert.Empty(Assert.Single((await c.Client.GetFromJsonAsync<OdontogramDetail>(ExamPath(e.Exam.Id)))!.Findings).SurfaceIds);
    }
    [Theory][InlineData(99,3,2)][InlineData(46,999,2)][InlineData(46,3,99)][InlineData(46,11,2)][InlineData(55,3,2)]
    public async Task InvalidFindingRejected(int tooth,int type,int surface) {
        var c=await Setup();var e=await Odo(c);Assert.Equal(HttpStatusCode.BadRequest,(await c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id),Findings(new FindingInput(tooth,type,[surface],null)))).StatusCode);
    }
    [Fact] public async Task SurfaceRequiredDuplicateSurfacesAndFindingsRejected() {
        var c=await Setup();var e=await Odo(c);
        foreach(var findings in new FindingInput[][] { [new(46,3,[],null)], [new(46,3,[2,2],null)], [new(46,3,[2],null),new(46,3,[2],null)] })
            Assert.Equal(HttpStatusCode.BadRequest,(await c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id),Findings(findings))).StatusCode);
    }
    [Theory][InlineData("odontograms")][InlineData("periodontal-exams")]
    public async Task EncounterAndPatientOwnershipEnforced(string kind) {
        var a=await Setup();var b=await Setup();var otherEncounter=await Encounter(b);
        Assert.Equal(HttpStatusCode.NotFound,(await a.Client.PostAsJsonAsync(PathFor(b,kind),new ExamCreateRequest("PERMANENT",February,null,null))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await a.Client.PostAsJsonAsync(PathFor(a,kind),new ExamCreateRequest("PERMANENT",February,otherEncounter,null))).StatusCode);
        var e=kind=="odontograms"?(await Odo(a)).Exam:(await Perio(a)).Exam;
        Assert.Equal(HttpStatusCode.NotFound,(await b.Client.GetAsync(ExamPath(e.Id,kind))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await b.Client.PostAsJsonAsync(ExamPath(e.Id,kind)+"/finalize",new ExamRevisionRequest(0))).StatusCode);
    }
    [Fact] public async Task FebruaryExactlyPreservedAfterSeptemberChangesAndFinalization() {
        var c=await Setup();var e=await Odo(c);var path=ExamPath(e.Exam.Id);
        await Ok(c.Client.PutAsJsonAsync(path,Findings(new FindingInput(46,3,[2],"February caries"))));
        await Ok(c.Client.PostAsJsonAsync(path+"/finalize",new ExamRevisionRequest(1)));
        var feb=(await c.Client.GetFromJsonAsync<OdontogramDetail>(path))!;Assert.Equal(c.Appointment.DoctorId,feb.Exam.FinalizedByDoctorProfileId);Assert.NotNull(feb.Exam.FinalizedAt);
        Assert.Equal(HttpStatusCode.Conflict,(await c.Client.PutAsJsonAsync(path,Findings())).StatusCode);
        var next=await Read<OdontogramDetail>(c.Client.PostAsJsonAsync(path+"/new-version",new ExamVersionRequest(September,null)));
        Assert.NotEqual(feb.Exam.Id,next.Exam.Id);Assert.Equal(feb.Exam.Id,next.Exam.PreviousExamId);Assert.NotEqual(feb.Findings[0].Id,next.Findings[0].Id);Assert.Equal(feb.Findings[0].SurfaceIds,next.Findings[0].SurfaceIds);
        await Ok(c.Client.PutAsJsonAsync(ExamPath(next.Exam.Id),Findings(new FindingInput(36,4,[2,4],"September restoration"))));
        await Ok(c.Client.PostAsJsonAsync(ExamPath(next.Exam.Id)+"/finalize",new ExamRevisionRequest(1)));
        var old=(await c.Client.GetFromJsonAsync<OdontogramDetail>(path))!;
        Assert.Equal(JsonSerializer.Serialize(feb.Findings),JsonSerializer.Serialize(old.Findings));Assert.Equal(feb.Exam with{IsLatest=false},old.Exam);
        var history=(await c.Client.GetFromJsonAsync<ExamSummary[]>(PathFor(c,"odontograms")))!;Assert.Equal(new[]{feb.Exam.Id,next.Exam.Id},history.Select(x=>x.Id));Assert.False(history[0].IsLatest);Assert.True(history[1].IsLatest);
        Assert.Equal(36,Assert.Single((await c.Client.GetFromJsonAsync<OdontogramDetail>(ExamPath(next.Exam.Id)))!.Findings).DentalToothId);
    }
    [Theory][InlineData("odontograms")][InlineData("periodontal-exams")]
    public async Task DoubleVersionRequestCreatesOnlyOneDraft(string kind) {
        var c=await Setup();var e=kind=="odontograms"?(await Odo(c)).Exam:(await Perio(c)).Exam;
        await Ok(c.Client.PostAsJsonAsync(ExamPath(e.Id,kind)+"/finalize",new ExamRevisionRequest(0)));
        var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>c.Client.PostAsJsonAsync(ExamPath(e.Id,kind)+"/new-version",new ExamVersionRequest(September,null))));
        Assert.All(responses,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));
        var ids=new List<Guid>();foreach(var r in responses){using var doc=JsonDocument.Parse(await r.Content.ReadAsStringAsync());ids.Add(doc.RootElement.GetProperty("exam").GetProperty("id").GetGuid());}Assert.Equal(ids[0],ids[1]);
        Assert.Equal(2,(await c.Client.GetFromJsonAsync<ExamSummary[]>(PathFor(c,kind)))!.Length);
    }
    [Theory][InlineData(3,2,5)][InlineData(4,-2,2)][InlineData(0,0,0)]
    public async Task SignedMarginAndDerivedCal(int probing,int margin,int expected) {
        var c=await Setup();var e=await Perio(c);await Ok(c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id,"periodontal-exams"),Measures(new MeasurementInput(46,1,probing,margin,true,true,true,null))));
        var m=Assert.Single((await c.Client.GetFromJsonAsync<PeriodontalDetail>(ExamPath(e.Exam.Id,"periodontal-exams")))!.Measurements);
        Assert.Equal((decimal)expected,m.ClinicalAttachmentLevelMm);Assert.True(m.BleedingOnProbing&&m.Plaque&&m.Suppuration);
    }
    [Theory][InlineData(null,null)][InlineData(3,null)][InlineData(null,2)]
    public async Task NotMeasuredNeverBecomesZero(int? probing,int? margin) {
        var c=await Setup();var e=await Perio(c);Assert.Empty(e.Measurements);
        await Ok(c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id,"periodontal-exams"),Measures(new MeasurementInput(46,1,probing,margin,false,false,false,null))));
        var m=Assert.Single((await c.Client.GetFromJsonAsync<PeriodontalDetail>(ExamPath(e.Exam.Id,"periodontal-exams")))!.Measurements);
        Assert.Equal((decimal?)probing,m.ProbingDepthMm);Assert.Equal((decimal?)margin,m.GingivalMarginMm);Assert.Null(m.ClinicalAttachmentLevelMm);
    }
    [Theory][InlineData(-1,0)][InlineData(16,0)][InlineData(3,-11)][InlineData(3,21)]
    public async Task InvalidMeasurementsRejected(int probing,int margin) {
        var c=await Setup();var e=await Perio(c);Assert.Equal(HttpStatusCode.BadRequest,(await c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id,"periodontal-exams"),Measures(new MeasurementInput(46,1,probing,margin,false,false,false,null)))).StatusCode);
    }
    [Fact] public async Task SixSitesUniqueAndInvalidSiteRejected() {
        var c=await Setup();var e=await Perio(c);var path=ExamPath(e.Exam.Id,"periodontal-exams");
        var m=new MeasurementInput(46,1,3,0,false,false,false,null);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.Client.PutAsJsonAsync(path,Measures(m,m))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.Client.PutAsJsonAsync(path,Measures(m with{PeriodontalSiteId=99}))).StatusCode);
        await Ok(c.Client.PutAsJsonAsync(path,Measures(Enumerable.Range(1,6).Select(i=>m with{PeriodontalSiteId=i}).ToArray())));
        Assert.Equal(6,(await c.Client.GetFromJsonAsync<PeriodontalDetail>(path))!.Measurements.Length);
    }
    [Theory][InlineData(-1)][InlineData(4)]
    public async Task InvalidMobilityRejected(int grade) {
        var c=await Setup();var e=await Perio(c);Assert.Equal(HttpStatusCode.BadRequest,(await c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id,"periodontal-exams"),Measures() with{ToothAssessments=[new(46,grade,null)]})).StatusCode);
    }
    [Theory][InlineData(11,"BUCCAL",1)][InlineData(46,"RANDOM",1)][InlineData(46,"BUCCAL",4)]
    public async Task InvalidFurcationRejected(int tooth,string location,int grade) {
        var c=await Setup();var e=await Perio(c);Assert.Equal(HttpStatusCode.BadRequest,(await c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id,"periodontal-exams"),Measures() with{Furcations=[new(tooth,location,grade,null)]})).StatusCode);
    }
    [Fact] public async Task PeriodontalSnapshotCopiesAllThreeChildrenAndPreservesHistory() {
        var c=await Setup();var e=await Perio(c);var path=ExamPath(e.Exam.Id,"periodontal-exams");
        var data=Measures(new MeasurementInput(46,1,4,2,true,false,false,"Historical")) with{ToothAssessments=[new(46,2,"Mobility")],Furcations=[new(46,"BUCCAL",2,"Furcation")]};
        await Ok(c.Client.PutAsJsonAsync(path,data));await Ok(c.Client.PostAsJsonAsync(path+"/finalize",new ExamRevisionRequest(1)));
        var feb=(await c.Client.GetFromJsonAsync<PeriodontalDetail>(path))!;
        Assert.Equal(HttpStatusCode.Conflict,(await c.Client.PutAsJsonAsync(path,data with{Revision=2})).StatusCode);
        var sep=await Read<PeriodontalDetail>(c.Client.PostAsJsonAsync(path+"/new-version",new ExamVersionRequest(September,null)));
        Assert.Equal(feb.Exam.Id,sep.Exam.PreviousExamId);Assert.Equal(feb.Measurements[0] with{Id=sep.Measurements[0].Id},sep.Measurements[0]);Assert.Equal(feb.ToothAssessments,sep.ToothAssessments);Assert.Equal(feb.Furcations,sep.Furcations);
        await Ok(c.Client.PutAsJsonAsync(ExamPath(sep.Exam.Id,"periodontal-exams"),Measures()));await Ok(c.Client.PostAsJsonAsync(ExamPath(sep.Exam.Id,"periodontal-exams")+"/finalize",new ExamRevisionRequest(1)));
        var old=(await c.Client.GetFromJsonAsync<PeriodontalDetail>(path))!;Assert.Equal(JsonSerializer.Serialize(feb.Measurements),JsonSerializer.Serialize(old.Measurements));Assert.Equal(feb.ToothAssessments,old.ToothAssessments);Assert.Equal(feb.Furcations,old.Furcations);
        Assert.Equal(new[]{feb.Exam.Id,sep.Exam.Id},(await c.Client.GetFromJsonAsync<ExamSummary[]>(PathFor(c,"periodontal-exams")))!.Select(x=>x.Id));
    }
    [Fact] public async Task StaleRevisionAndVersionOfDraftRejected() {
        var c=await Setup();var e=await Odo(c);await Ok(c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id),Findings()));
        Assert.Equal(HttpStatusCode.Conflict,(await c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id),Findings())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await c.Client.PostAsJsonAsync(ExamPath(e.Exam.Id)+"/new-version",new ExamVersionRequest(September,null))).StatusCode);
    }
    [Fact] public async Task NoSessionOrNonDoctorCannotUseCharting() {
        using var client=f.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/doctor/dental-catalogs/teeth")).StatusCode);
        var login=await f.Login(client);client.DefaultRequestHeaders.Authorization=new("Bearer",login.Body.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync("/api/doctor/dental-catalogs/teeth")).StatusCode);
    }
    [Fact] public async Task AuditContainsOnlyIdentifiersNotClinicalText() {
        var c=await Setup();var e=await Odo(c);await Ok(c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id),Findings(new FindingInput(46,3,[2],"SECRET_CLINICAL_TEXT"))));
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs=await db.AuditLogs.Where(x=>x.EntityId==e.Exam.Id).ToArrayAsync();Assert.Equal(2,logs.Length);
        foreach(var log in logs){Assert.DoesNotContain("SECRET",log.MetadataJson!);using var json=JsonDocument.Parse(log.MetadataJson!);Assert.All(json.RootElement.EnumerateObject(),p=>Assert.Contains(p.Name,new[]{"ExamId","PatientProfileId","ClinicalEncounterId","PreviousExamId","DentitionMode"}));}
    }
    [Fact] public async Task PostgreSqlProtectsFinalizedRootFindingsAndSurfaces() {
        var c=await Setup();var e=await Odo(c);await Ok(c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id),Findings(new FindingInput(46,3,[2],null))));
        await Ok(c.Client.PostAsJsonAsync(ExamPath(e.Exam.Id)+"/finalize",new ExamRevisionRequest(1)));
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var finding=await db.Set<OdontogramFinding>().SingleAsync(x=>x.OdontogramExamId==e.Exam.Id);
        var root=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"OdontogramExams\" SET \"GeneralNotes\"='tamper' WHERE \"Id\"={e.Exam.Id}"));Assert.Equal("23514",root.SqlState);
        var child=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"OdontogramFindings\" SET \"Observation\"='tamper' WHERE \"Id\"={finding.Id}"));Assert.Equal("23514",child.SqlState);
        var surface=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"OdontogramFindingSurfaces\" WHERE \"OdontogramFindingId\"={finding.Id}"));Assert.Equal("23514",surface.SqlState);
        var delete=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"OdontogramExams\" WHERE \"Id\"={e.Exam.Id}"));Assert.Equal("23514",delete.SqlState);
    }
    [Fact] public async Task CopyFailureRollsBackRootChildrenAndAudit() {
        var c=await Setup();var e=await Odo(c);await Ok(c.Client.PutAsJsonAsync(ExamPath(e.Exam.Id),Findings(new FindingInput(46,3,[2],"FAIL_COPY"))));
        await Ok(c.Client.PostAsJsonAsync(ExamPath(e.Exam.Id)+"/finalize",new ExamRevisionRequest(1)));
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION test_chart_copy_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"Observation\"='FAIL_COPY' THEN RAISE EXCEPTION 'injected failure' USING ERRCODE='23514'; END IF; RETURN NEW; END $$; CREATE TRIGGER test_chart_copy_failure BEFORE INSERT ON \"OdontogramFindings\" FOR EACH ROW EXECUTE FUNCTION test_chart_copy_failure();");
        try {
            Assert.Equal(HttpStatusCode.Conflict,(await c.Client.PostAsJsonAsync(ExamPath(e.Exam.Id)+"/new-version",new ExamVersionRequest(September,null))).StatusCode);
            Assert.Equal(1,await db.Set<OdontogramExam>().CountAsync(x=>x.PatientProfileId==c.Appointment.PatientId));
            Assert.False(await db.AuditLogs.AnyAsync(x=>x.Action=="odontogram_version_created"&&x.UserId==c.User));
        } finally {await db.Database.ExecuteSqlRawAsync("DROP TRIGGER test_chart_copy_failure ON \"OdontogramFindings\"; DROP FUNCTION test_chart_copy_failure();");}
    }
    [Fact] public async Task PostgreSqlProtectsAllFinalizedPeriodontalChildren() {
        var c=await Setup();var e=await Perio(c);var path=ExamPath(e.Exam.Id,"periodontal-exams");
        await Ok(c.Client.PutAsJsonAsync(path,Measures(new MeasurementInput(46,1,3,0,false,false,false,null)) with{ToothAssessments=[new(46,1,null)],Furcations=[new(46,"BUCCAL",1,null)]}));
        await Ok(c.Client.PostAsJsonAsync(path+"/finalize",new ExamRevisionRequest(1)));
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach(var table in new[]{"PeriodontalSiteMeasurements","PeriodontalToothAssessments","PeriodontalFurcationAssessments"}) {
            // Table names are fixed test constants; the exam identifier is parameterized.
            var error=await Assert.ThrowsAsync<PostgresException>(()=>db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table}\" WHERE \"PeriodontalExamId\"={{0}}",e.Exam.Id));
            Assert.Equal("23514",error.SqlState);
        }
    }
    [Theory][InlineData("odontograms")][InlineData("periodontal-exams")]
    public async Task AnotherTreatingDoctorCanReadButCannotFinalizeAuthorsDraft(string kind) {
        var a=await Setup();var b=await Setup();
        using(var scope=f.Services.CreateScope()) {
            var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Appointments.Add(new Appointment{DoctorId=b.Appointment.DoctorId,PatientId=a.Appointment.PatientId,CenterId=a.Appointment.CenterId,SpecialtyId=a.Appointment.SpecialtyId,AppointmentDate=new(2026,10,15),StartTime=new(9,0),EndTime=new(9,30)});await db.SaveChangesAsync();
        }
        var e=kind=="odontograms"?(await Odo(a)).Exam:(await Perio(a)).Exam;
        await Ok(b.Client.GetAsync(ExamPath(e.Id,kind)));
        Assert.Equal(HttpStatusCode.Forbidden,(await b.Client.PostAsJsonAsync(ExamPath(e.Id,kind)+"/finalize",new ExamRevisionRequest(0))).StatusCode);
    }
    [Fact] public void AllNewForeignKeysRestrictAndNoJsonOrStoredCal() {
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entities=db.Model.GetEntityTypes().Where(x=>x.ClrType.Namespace==typeof(OdontogramExam).Namespace).ToArray();Assert.Equal(11,entities.Length);
        Assert.All(entities.SelectMany(x=>x.GetForeignKeys()),fk=>Assert.Equal(DeleteBehavior.Restrict,fk.DeleteBehavior));
        Assert.DoesNotContain(entities.SelectMany(x=>x.GetProperties()),p=>p.GetColumnType()?.Contains("json")==true || p.Name=="ClinicalAttachmentLevelMm");
    }
}
