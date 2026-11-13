using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.ClinicalCare;
// Shared read model used by Cashier and the authenticated patient portal.
public sealed class FinancialAccountRow
{
 public Guid Plan {get;set;} public Guid Episode {get;set;} public Guid Patient {get;set;}
 public string Name {get;set;} = ""; public string? DocumentType {get;set;} public string? DocumentNumber {get;set;}
 public string? DocumentCountryCode {get;set;} public string? Phone {get;set;} public DateOnly? BirthDate {get;set;}
 public string Title {get;set;} = ""; public string Doctor {get;set;} = ""; public DateTime StartedAt {get;set;}
 public decimal AgreedTotal {get;set;} public decimal Paid {get;set;}
}
public static class FinancialAccountRead
{
 public static IQueryable<FinancialAccountRow> Query(AppDbContext db, Guid? patient=null, Guid? plan=null)
 {
        return from account in db.Set<TreatmentFinancialAccount>().AsNoTracking()
            join p in db.Set<TreatmentPlan>() on account.TreatmentPlanId equals p.Id
            join e in db.Set<TreatmentEpisode>() on p.TreatmentEpisodeId equals e.Id
            join patientProfile in db.PatientProfiles on e.PatientProfileId equals patientProfile.Id
            join u in db.Users on patientProfile.UserId equals u.Id
            join d in db.DoctorProfiles on e.PrimaryDoctorProfileId equals d.Id
            join doctor in db.Users on d.UserId equals doctor.Id
            where (patient==null||e.PatientProfileId==patient)&&(plan==null||p.Id==plan)
            orderby e.StartedAt descending,p.Id
            select new FinancialAccountRow {Plan=p.Id,Episode=e.Id,Patient=patientProfile.Id,Name=u.FirstName+" "+u.LastName,DocumentType=u.DocumentType, DocumentNumber=u.DocumentNumber, DocumentCountryCode=u.DocumentCountryCode,Phone=u.Phone,BirthDate=u.BirthDate,Title=e.Title,Doctor=doctor.FirstName+" "+doctor.LastName,StartedAt=e.StartedAt,AgreedTotal=account.AgreedTotal,Paid=db.Set<TreatmentPayment>().Where(x=>x.FinancialAccountId==account.Id).Sum(x=>(decimal?)x.Amount)??0m};
 }
}
