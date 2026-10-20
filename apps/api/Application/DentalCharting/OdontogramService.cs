using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.DentalCharting;
using ClinicaRamirez.Api.DTOs.DentalCharting;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.DentalCharting;

public sealed class OdontogramService(AppDbContext db, ChartingAccess access, ChartExamStore<OdontogramExam> exams)
{
    public Task<ExamSummary[]> History(Guid patient) => exams.History(patient);
    public async Task<OdontogramDetail> Get(Guid id) {
        var e=await exams.Read(id);
        var findings=await db.Set<OdontogramFinding>().AsNoTracking().Where(x=>x.OdontogramExamId==id).OrderBy(x=>x.DentalToothId).ThenBy(x=>x.Id).ToArrayAsync();
        var ids=findings.Select(x=>x.Id).ToArray();
        var surfaces=await db.Set<OdontogramFindingSurface>().AsNoTracking().Where(x=>ids.Contains(x.OdontogramFindingId)).ToArrayAsync();
        return new(await exams.Summary(e),findings.Select(x=>new FindingDto(x.Id,x.DentalToothId,x.FindingTypeId,surfaces.Where(s=>s.OdontogramFindingId==x.Id).Select(s=>s.DentalSurfaceId).Order().ToArray(),x.Observation)).ToArray());
    }
    public Task<Guid> Create(Guid patient, ExamCreateRequest input) => access.Write(async()=> {
        var e=await exams.Create(patient,input); access.Audit("odontogram_created",e); return e.Id;
    });
    public Task<Guid> Version(Guid id, ExamVersionRequest input) => access.Write(async()=> {
        var result=await exams.Version(id,input,async(previous,next)=> {
            var findings=await db.Set<OdontogramFinding>().AsNoTracking().Where(x=>x.OdontogramExamId==previous.Id).ToArrayAsync();
            var ids=findings.Select(x=>x.Id).ToArray();
            var surfaces=await db.Set<OdontogramFindingSurface>().AsNoTracking().Where(x=>ids.Contains(x.OdontogramFindingId)).ToArrayAsync();
            foreach(var old in findings) {
                var finding=new OdontogramFinding{OdontogramExamId=next.Id,DentalToothId=old.DentalToothId,FindingTypeId=old.FindingTypeId,Observation=old.Observation};db.Add(finding);
                foreach(var surface in surfaces.Where(x=>x.OdontogramFindingId==old.Id)) db.Add(new OdontogramFindingSurface{OdontogramFindingId=finding.Id,DentalSurfaceId=surface.DentalSurfaceId});
            }
        });
        if(result.Created) access.Audit("odontogram_version_created",result.Exam); return result.Exam.Id;
    });
    public async Task Update(Guid id, OdontogramUpdateRequest input) => await access.Write(async()=> {
        var e=await exams.Editable(id,input.Revision); ChartingAccess.Mode(input.DentitionMode); ChartingAccess.Text(input.GeneralNotes);
        if(input.Findings is null || input.Findings.Length>ChartingRules.MaxItems || input.Findings.Any(x=>x is null)) throw new ApiException(400,"Hallazgos inválidos.");
        await access.ValidateTeeth(input.Findings.Select(x=>x.DentalToothId),input.DentitionMode);
        var types=await db.Set<OdontogramFindingType>().Where(x=>x.IsActive).ToDictionaryAsync(x=>x.Id);
        var surfaces=await db.Set<DentalSurface>().Where(x=>x.IsActive).Select(x=>x.Id).ToArrayAsync();
        foreach(var f in input.Findings) {
            ChartingAccess.Text(f.Observation);
            if(!types.TryGetValue(f.FindingTypeId,out var type)) throw new ApiException(400,"Tipo de hallazgo inexistente o inactivo.");
            if(f.SurfaceIds is null || f.SurfaceIds.Length!=f.SurfaceIds.Distinct().Count() || f.SurfaceIds.Except(surfaces).Any()) throw new ApiException(400,"Superficies inválidas o duplicadas.");
            if(f.SurfaceIds.Length==0 ? !type.AppliesToWholeTooth : !type.AppliesToSurface) throw new ApiException(400,"Las superficies no corresponden al alcance del hallazgo.");
        }
        foreach(var group in input.Findings.GroupBy(x=>x.DentalToothId)) {
            if(group.Count()>1 && group.Any(x=>types[x.FindingTypeId].Code=="MISSING_TOOTH")) throw new ApiException(400,"Un diente ausente no admite otros hallazgos simultáneos.");
            var records=group.ToArray();
            for(var i=0;i<records.Length;i++) for(var j=i+1;j<records.Length;j++) if(records[i].FindingTypeId==records[j].FindingTypeId && records[i].SurfaceIds.Order().SequenceEqual(records[j].SurfaceIds.Order())) throw new ApiException(400,"Hallazgo duplicado.");
        }
        var old=await db.Set<OdontogramFinding>().Where(x=>x.OdontogramExamId==id).ToArrayAsync();var ids=old.Select(x=>x.Id).ToArray();
        db.RemoveRange(await db.Set<OdontogramFindingSurface>().Where(x=>ids.Contains(x.OdontogramFindingId)).ToArrayAsync()); db.RemoveRange(old);
        await db.SaveChangesAsync(); // Explicit bridge-before-parent deletion; still inside the transaction, never CASCADE.
        foreach(var f in input.Findings) {
            var finding=new OdontogramFinding{OdontogramExamId=id,DentalToothId=f.DentalToothId,FindingTypeId=f.FindingTypeId,Observation=f.Observation};db.Add(finding);
            foreach(var surface in f.SurfaceIds) db.Add(new OdontogramFindingSurface{OdontogramFindingId=finding.Id,DentalSurfaceId=surface});
        }
        e.DentitionMode=input.DentitionMode;e.GeneralNotes=input.GeneralNotes;ChartExamStore<OdontogramExam>.Changed(e);access.Audit("odontogram_updated",e);return true;
    });
    public Task Finalize(Guid id, ExamRevisionRequest input) => exams.Finalize(id,input.Revision,"odontogram_finalized");
}
