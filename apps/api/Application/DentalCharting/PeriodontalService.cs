using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.DentalCharting;
using ClinicaRamirez.Api.DTOs.DentalCharting;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.DentalCharting;

public sealed class PeriodontalService(AppDbContext db, ChartingAccess access, ChartExamStore<PeriodontalExam> exams)
{
    public Task<ExamSummary[]> History(Guid patient)=>exams.History(patient);
    public async Task<PeriodontalDetail> Get(Guid id) {
        var e=await exams.Read(id);
        return new(await exams.Summary(e),await db.Set<PeriodontalSiteMeasurement>().AsNoTracking().Where(x=>x.PeriodontalExamId==id).OrderBy(x=>x.DentalToothId).ThenBy(x=>x.PeriodontalSiteId)
            .Select(x=>new MeasurementDto(x.Id,x.DentalToothId,x.PeriodontalSiteId,x.ProbingDepthMm,x.GingivalMarginMm,
                x.ProbingDepthMm.HasValue && x.GingivalMarginMm.HasValue ? x.ProbingDepthMm+x.GingivalMarginMm : null,x.BleedingOnProbing,x.Plaque,x.Suppuration,x.Observation)).ToArrayAsync(),
            await db.Set<PeriodontalToothAssessment>().AsNoTracking().Where(x=>x.PeriodontalExamId==id).OrderBy(x=>x.DentalToothId).Select(x=>new ToothAssessmentInput(x.DentalToothId,x.MobilityGrade,x.Observation)).ToArrayAsync(),
            await db.Set<PeriodontalFurcationAssessment>().AsNoTracking().Where(x=>x.PeriodontalExamId==id).OrderBy(x=>x.DentalToothId).ThenBy(x=>x.Location).Select(x=>new FurcationInput(x.DentalToothId,x.Location,x.Grade,x.Observation)).ToArrayAsync());
    }
    public Task<Guid> Create(Guid patient,ExamCreateRequest input)=>access.Write(async()=>{var e=await exams.Create(patient,input);access.Audit("periodontal_exam_created",e);return e.Id;});
    public Task<Guid> Version(Guid id,ExamVersionRequest input)=>access.Write(async()=> {
        var result=await exams.Version(id,input,async(previous,next)=> {
            foreach(var x in await db.Set<PeriodontalSiteMeasurement>().AsNoTracking().Where(x=>x.PeriodontalExamId==previous.Id).ToArrayAsync())
                db.Add(new PeriodontalSiteMeasurement{PeriodontalExamId=next.Id,DentalToothId=x.DentalToothId,PeriodontalSiteId=x.PeriodontalSiteId,ProbingDepthMm=x.ProbingDepthMm,GingivalMarginMm=x.GingivalMarginMm,BleedingOnProbing=x.BleedingOnProbing,Plaque=x.Plaque,Suppuration=x.Suppuration,Observation=x.Observation});
            foreach(var x in await db.Set<PeriodontalToothAssessment>().AsNoTracking().Where(x=>x.PeriodontalExamId==previous.Id).ToArrayAsync())
                db.Add(new PeriodontalToothAssessment{PeriodontalExamId=next.Id,DentalToothId=x.DentalToothId,MobilityGrade=x.MobilityGrade,Observation=x.Observation});
            foreach(var x in await db.Set<PeriodontalFurcationAssessment>().AsNoTracking().Where(x=>x.PeriodontalExamId==previous.Id).ToArrayAsync())
                db.Add(new PeriodontalFurcationAssessment{PeriodontalExamId=next.Id,DentalToothId=x.DentalToothId,Location=x.Location,Grade=x.Grade,Observation=x.Observation});
        });if(result.Created)access.Audit("periodontal_exam_version_created",result.Exam);return result.Exam.Id;
    });
    public async Task Update(Guid id,PeriodontalUpdateRequest input)=>await access.Write(async()=> {
        var e=await exams.Editable(id,input.Revision);ChartingAccess.Mode(input.DentitionMode);ChartingAccess.Text(input.GeneralNotes);
        if(input.Measurements is null || input.ToothAssessments is null || input.Furcations is null || input.Measurements.Length>312 || input.ToothAssessments.Length>52 || input.Furcations.Length>208 || input.Measurements.Any(x=>x is null) || input.ToothAssessments.Any(x=>x is null) || input.Furcations.Any(x=>x is null)) throw new ApiException(400,"Datos periodontales inválidos.");
        await access.ValidateTeeth(input.Measurements.Select(x=>x.DentalToothId).Concat(input.ToothAssessments.Select(x=>x.DentalToothId)).Concat(input.Furcations.Select(x=>x.DentalToothId)),input.DentitionMode);
        if(input.Measurements.Select(x=>(x.DentalToothId,x.PeriodontalSiteId)).Distinct().Count()!=input.Measurements.Length || input.ToothAssessments.Select(x=>x.DentalToothId).Distinct().Count()!=input.ToothAssessments.Length || input.Furcations.Select(x=>(x.DentalToothId,x.Location)).Distinct().Count()!=input.Furcations.Length) throw new ApiException(400,"Mediciones o evaluaciones duplicadas.");
        var sites=await db.Set<PeriodontalSite>().Where(x=>x.IsActive).Select(x=>x.Id).ToArrayAsync();
        foreach(var m in input.Measurements) {
            if(!sites.Contains(m.PeriodontalSiteId)) throw new ApiException(400,"Sitio periodontal inexistente o inactivo.");
            Range(m.ProbingDepthMm,0,ChartingRules.MaxProbingDepth);Range(m.GingivalMarginMm,ChartingRules.MinMargin,ChartingRules.MaxMargin);ChartingAccess.Text(m.Observation);
        }
        foreach(var t in input.ToothAssessments) {Range(t.MobilityGrade,0,ChartingRules.MaxMobility);ChartingAccess.Text(t.Observation);}
        var teeth=await db.Set<DentalTooth>().ToDictionaryAsync(x=>x.Id);
        foreach(var f in input.Furcations) {
            // Incisors/canines do not have a furcation. Root anatomy in premolars/molars is assessed by the clinician.
            if(!ChartingRules.FurcationLocations.Contains(f.Location) || teeth[f.DentalToothId].ToothType is not ("MOLAR" or "PREMOLAR")) throw new ApiException(400,"Furcación incompatible con el diente o ubicación inválida.");
            Range(f.Grade,0,ChartingRules.MaxFurcation);ChartingAccess.Text(f.Observation);
        }
        db.RemoveRange(await db.Set<PeriodontalSiteMeasurement>().Where(x=>x.PeriodontalExamId==id).ToArrayAsync());
        db.RemoveRange(await db.Set<PeriodontalToothAssessment>().Where(x=>x.PeriodontalExamId==id).ToArrayAsync());
        db.RemoveRange(await db.Set<PeriodontalFurcationAssessment>().Where(x=>x.PeriodontalExamId==id).ToArrayAsync());
        await db.SaveChangesAsync();
        foreach(var m in input.Measurements) db.Add(new PeriodontalSiteMeasurement{PeriodontalExamId=id,DentalToothId=m.DentalToothId,PeriodontalSiteId=m.PeriodontalSiteId,ProbingDepthMm=m.ProbingDepthMm,GingivalMarginMm=m.GingivalMarginMm,BleedingOnProbing=m.BleedingOnProbing,Plaque=m.Plaque,Suppuration=m.Suppuration,Observation=m.Observation});
        foreach(var t in input.ToothAssessments) db.Add(new PeriodontalToothAssessment{PeriodontalExamId=id,DentalToothId=t.DentalToothId,MobilityGrade=t.MobilityGrade,Observation=t.Observation});
        foreach(var f in input.Furcations) db.Add(new PeriodontalFurcationAssessment{PeriodontalExamId=id,DentalToothId=f.DentalToothId,Location=f.Location,Grade=f.Grade,Observation=f.Observation});
        e.DentitionMode=input.DentitionMode;e.GeneralNotes=input.GeneralNotes;ChartExamStore<PeriodontalExam>.Changed(e);access.Audit("periodontal_exam_updated",e);return true;
    });
    private static void Range(decimal? n,int min,int max) {if(n.HasValue && (n<min || n>max || decimal.Round(n.Value,1)!=n))throw new ApiException(400,"Medición fuera de rango o con más de un decimal.");}
    public Task Finalize(Guid id,ExamRevisionRequest input)=>exams.Finalize(id,input.Revision,"periodontal_exam_finalized");
}
