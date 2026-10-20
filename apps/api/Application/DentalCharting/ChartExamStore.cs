using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.DentalCharting;
using ClinicaRamirez.Api.DTOs.DentalCharting;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.DentalCharting;

// Shared lifecycle only; findings and periodontal measurements remain separate aggregates/services.
public sealed class ChartExamStore<T>(AppDbContext db, ChartingAccess access) where T : class, IChartExam, new()
{
    public async Task<T> Read(Guid id) {
        var e = await db.Set<T>().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Examen inexistente.");
        await access.Patient(e.PatientProfileId); return e;
    }
    public async Task<T> Editable(Guid id, int revision) {
        var e = await Read(id);
        if (e.CreatedByDoctorProfileId != await access.Doctor()) throw new ApiException(403, "Solo el doctor autor puede modificar este examen.");
        if (e.Status != "DRAFT") throw new ApiException(409, "El examen está finalizado; crea una nueva versión.");
        if (e.Revision != revision) throw new ApiException(409, "El examen cambió. Recarga antes de guardar.");
        return e;
    }
    public async Task<T> Create(Guid patient, ExamCreateRequest input) {
        var doctor = await access.Patient(patient); ChartingAccess.Mode(input.DentitionMode); ChartingAccess.Text(input.GeneralNotes);
        if (input.ClinicalDate == default) throw new ApiException(400, "Fecha clínica obligatoria.");
        await access.Encounter(input.ClinicalEncounterId, patient, doctor);
        if (await db.Set<T>().AnyAsync(x => x.PatientProfileId == patient)) throw new ApiException(409, "Ya existe un examen; utiliza nueva versión del último finalizado.");
        var e = new T { PatientProfileId=patient, ClinicalEncounterId=input.ClinicalEncounterId, DentitionMode=input.DentitionMode, ClinicalDate=input.ClinicalDate.UtcDateTime, CreatedByDoctorProfileId=doctor, GeneralNotes=input.GeneralNotes };
        db.Add(e); return e;
    }
    public async Task<(T Exam, bool Created)> Version(Guid previousId, ExamVersionRequest input, Func<T,T,Task> copy) {
        var previous = await Read(previousId); var doctor = await access.Doctor();
        if (previous.Status != "FINALIZED") throw new ApiException(409, "La versión anterior debe estar finalizada.");
        if (input.ClinicalDate == default || input.ClinicalDate.UtcDateTime < previous.ClinicalDate) throw new ApiException(400, "La fecha no puede preceder al examen anterior.");
        await access.Encounter(input.ClinicalEncounterId, previous.PatientProfileId, doctor);
        var successor = await db.Set<T>().SingleOrDefaultAsync(x => x.PreviousExamId == previousId);
        if (successor is not null) {
            if (successor.Status == "DRAFT" && successor.CreatedByDoctorProfileId == doctor && successor.ClinicalEncounterId == input.ClinicalEncounterId && successor.ClinicalDate == input.ClinicalDate.UtcDateTime) return (successor,false);
            throw new ApiException(409, "Ya existe una versión posterior; utiliza el último examen.");
        }
        if (await db.Set<T>().AnyAsync(x => x.PatientProfileId == previous.PatientProfileId && x.Status == "DRAFT")) throw new ApiException(409,"Ya existe un borrador.");
        var e = new T { PatientProfileId=previous.PatientProfileId, ClinicalEncounterId=input.ClinicalEncounterId, DentitionMode=previous.DentitionMode, ClinicalDate=input.ClinicalDate.UtcDateTime, PreviousExamId=previous.Id, CreatedByDoctorProfileId=doctor, GeneralNotes=previous.GeneralNotes };
        db.Add(e); await copy(previous,e); return (e,true);
    }
    public async Task Finalize(Guid id, int revision, string audit) {
        await access.Write(async () => { var e=await Editable(id,revision); e.Status="FINALIZED"; e.FinalizedAt=DateTime.UtcNow; e.FinalizedByDoctorProfileId=await access.Doctor(); Changed(e); access.Audit(audit,e); return true; });
    }
    public static void Changed(T e) { e.Revision++; e.UpdatedAt=DateTime.UtcNow; }
    public async Task<ExamSummary> Summary(T e) {
        var name = await db.DoctorProfiles.Where(x => x.Id == e.CreatedByDoctorProfileId).Select(x => x.User.FirstName+" "+x.User.LastName).SingleAsync();
        return new(e.Id,e.PatientProfileId,e.ClinicalEncounterId,e.ClinicalDate,e.DentitionMode,e.Status,name,e.PreviousExamId,
            !await db.Set<T>().AnyAsync(x=>x.PreviousExamId==e.Id),e.Revision,e.GeneralNotes,e.FinalizedAt,e.FinalizedByDoctorProfileId);
    }
    public async Task<ExamSummary[]> History(Guid patient) {
        await access.Patient(patient); var exams=await db.Set<T>().AsNoTracking().Where(x=>x.PatientProfileId==patient).OrderBy(x=>x.ClinicalDate).ThenBy(x=>x.CreatedAt).ToArrayAsync();
        var results=new List<ExamSummary>(); foreach(var e in exams) results.Add(await Summary(e)); return results.ToArray();
    }
}
