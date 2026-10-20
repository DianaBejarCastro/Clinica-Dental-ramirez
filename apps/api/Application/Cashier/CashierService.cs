using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Application.ClinicalCare;
using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.DTOs;
using ClinicaRamirez.Api.DTOs.Cashier;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.Cashier;
public sealed class CashierService(AppDbContext db,FinancialPayments payments)
{
    public async Task<CashierPatient[]> Search(string? q,int limit)
    {
        q=q?.Trim();if(string.IsNullOrEmpty(q)||q.Length<2||q.Length>200||limit<1||limit>10)throw new ApiException(400,"Busca entre 2 y 200 caracteres; máximo 10 resultados.");
        var term=q.ToLowerInvariant();
        return await db.PatientProfiles.AsNoTracking().Where(x=>(x.User.FirstName+" "+x.User.LastName).ToLower().Contains(term)
            ||(x.User.Ci!=null&&x.User.Ci.ToLower().Contains(term))||(x.User.Phone!=null&&x.User.Phone.Contains(term))||(x.User.Email!=null&&x.User.Email.ToLower().Contains(term)))
            .OrderBy(x=>x.User.LastName).ThenBy(x=>x.User.FirstName).ThenBy(x=>x.Id).Take(limit)
            .Select(x=>new CashierPatient(x.Id,x.User.FirstName,x.User.LastName,x.User.Ci,x.User.Phone,x.User.Email)).ToArrayAsync();
    }
    private async Task PatientExists(Guid id){if(!await db.PatientProfiles.AnyAsync(x=>x.Id==id))throw new ApiException(404,"Paciente no disponible.");}
    private async Task<CashierTreatment[]> Summaries(Guid? patient=null,Guid? plan=null)
    {
        var rows=await (from account in db.Set<TreatmentFinancialAccount>().AsNoTracking()
            join p in db.Set<TreatmentPlan>() on account.TreatmentPlanId equals p.Id
            join e in db.Set<TreatmentEpisode>() on p.TreatmentEpisodeId equals e.Id
            join patientProfile in db.PatientProfiles on e.PatientProfileId equals patientProfile.Id
            join u in db.Users on patientProfile.UserId equals u.Id
            join d in db.DoctorProfiles on e.PrimaryDoctorProfileId equals d.Id
            join doctor in db.Users on d.UserId equals doctor.Id
            where (patient==null||e.PatientProfileId==patient)&&(plan==null||p.Id==plan)
            orderby e.StartedAt descending,p.Id
            select new{Plan=p.Id,Episode=e.Id,Patient=patientProfile.Id,Name=u.FirstName+" "+u.LastName,u.Ci,u.Phone,u.BirthDate,Title=e.Title,Doctor=doctor.FirstName+" "+doctor.LastName,e.StartedAt,account.AgreedTotal,Paid=db.Set<TreatmentPayment>().Where(x=>x.FinancialAccountId==account.Id).Sum(x=>(decimal?)x.Amount)??0m}).ToArrayAsync();
        var today=LocalBirthDateAttribute.Today;
        if(rows.Any(x=>x.Paid>x.AgreedTotal))throw new ApiException(409,"La cuenta requiere revisión financiera: saldo inconsistente.");
        return rows.Select(x=>new CashierTreatment(x.Plan,x.Episode,x.Patient,x.Name,x.Ci,x.Phone,x.BirthDate is DateOnly birth?today.Year-birth.Year-(birth.AddYears(today.Year-birth.Year)>today?1:0):null,x.Title,x.Doctor,x.StartedAt,x.AgreedTotal,x.Paid,x.AgreedTotal-x.Paid,x.Paid==x.AgreedTotal?"PAID":"PENDING")).ToArray();
    }
    private Task<CashierPayment[]> History(Guid? patient=null,Guid? plan=null)=>
        (from payment in db.Set<TreatmentPayment>().AsNoTracking()
         join account in db.Set<TreatmentFinancialAccount>() on payment.FinancialAccountId equals account.Id
         join p in db.Set<TreatmentPlan>() on account.TreatmentPlanId equals p.Id
         join e in db.Set<TreatmentEpisode>() on p.TreatmentEpisodeId equals e.Id
         join d in db.DoctorProfiles on e.PrimaryDoctorProfileId equals d.Id
         join doctor in db.Users on d.UserId equals doctor.Id
         join recorder in db.Users on payment.RecordedByUserId equals recorder.Id
         where (patient==null||e.PatientProfileId==patient)&&(plan==null||p.Id==plan)
         orderby payment.PaidAt descending,payment.Id
         select new CashierPayment(payment.Id,p.Id,e.Title,doctor.FirstName+" "+doctor.LastName,payment.Amount,payment.PaidAt,payment.PaymentMethod,payment.Reference,payment.Notes,recorder.FirstName+" "+recorder.LastName,"PAID")).ToArrayAsync();
    public async Task<CashierTreatment[]> Treatments(Guid patient){await PatientExists(patient);return await Summaries(patient:patient);}
    public async Task<CashierPayment[]> PatientPayments(Guid patient){await PatientExists(patient);return await History(patient:patient);}
    public async Task<CashierDetail> Detail(Guid plan)
    {
        var summary=(await Summaries(plan:plan)).SingleOrDefault()??throw new ApiException(404,"Tratamiento con cuenta financiera no disponible.");
        var items=await db.Set<TreatmentPlanItem>().AsNoTracking().Where(x=>x.TreatmentPlanId==plan).OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.Id)
            .Select(x=>new CashierProcedure(x.ProcedureName,x.UnitPrice,x.Quantity,x.UnitPrice*x.Quantity,x.Status)).ToArrayAsync();
        return new(summary,items,await History(plan:plan));
    }
    public async Task Pay(Guid plan,CashierPaymentRequest input)
    {
        await using var tx=await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(820030)");
        await payments.Record(plan,new(input.IdempotencyKey,input.Amount,input.PaymentMethod,input.Reference,input.Notes));
        await db.SaveChangesAsync();await tx.CommitAsync();
    }
}
