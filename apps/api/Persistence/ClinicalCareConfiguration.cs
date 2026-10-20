using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.Patients;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Appointments;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;
public static class ClinicalCareConfiguration
{
    public static void ConfigureClinicalCare(this ModelBuilder b)
    {
        var tables = new (Type Type, string Table)[] { (typeof(TreatmentEpisode),"TreatmentEpisodes"),(typeof(TreatmentAnamnesis),"TreatmentAnamneses"),(typeof(PatientMedicalHistoryReview),"PatientMedicalHistoryReviews"),(typeof(PatientAllergyEntry),"PatientAllergyEntries"),(typeof(PatientConditionEntry),"PatientConditionEntries"),(typeof(PatientMedicationEntry),"PatientMedicationEntries"),(typeof(ClinicalEncounter),"ClinicalEncounters"),(typeof(SoapNote),"SoapNotes"),(typeof(SoapAddendum),"SoapAddenda"),(typeof(TreatmentPlan),"TreatmentPlans"),(typeof(TreatmentPlanItem),"TreatmentPlanItems"),(typeof(TreatmentFinancialAccount),"TreatmentFinancialAccounts"),(typeof(TreatmentPayment),"TreatmentPayments") };
        foreach (var (type,table) in tables) {
            var e = b.Entity(type); e.ToTable(table); e.HasKey("Id");
            foreach (var p in type.GetProperties().Where(p=>p.PropertyType==typeof(string))) e.Property(p.Name).HasMaxLength(p.Name is "Status" ? 30 : p.Name is "Title" or "Name" or "ConditionName" or "MedicationName" or "ProcedureName" ? 200 : 8000);
        }
        void F<T,P>(string property) where T:class where P:class => b.Entity<T>().HasOne<P>().WithMany().HasForeignKey(property).OnDelete(DeleteBehavior.Restrict);
        void Check<T>(string table,string expression) where T:class => b.Entity<T>().ToTable(table,t=>t.HasCheckConstraint("CK_"+table+"_Domain",expression));
        F<TreatmentEpisode,PatientProfile>("PatientProfileId"); F<TreatmentEpisode,DoctorProfile>("PrimaryDoctorProfileId");
        Check<TreatmentEpisode>("TreatmentEpisodes","\"Status\" IN ('OPEN','COMPLETED','CANCELLED') AND length(btrim(\"Title\"))>0");
        F<TreatmentAnamnesis,TreatmentEpisode>("TreatmentEpisodeId"); F<TreatmentAnamnesis,User>("RecordedByUserId"); b.Entity<TreatmentAnamnesis>().HasIndex(x=>x.TreatmentEpisodeId).IsUnique();
        Check<TreatmentAnamnesis>("TreatmentAnamneses","length(btrim(\"ChiefComplaint\"))>0 AND length(btrim(\"PainSince\"))>0 AND (\"PainIntensity\" IS NULL OR \"PainIntensity\" BETWEEN 0 AND 10)");
        F<PatientMedicalHistoryReview,PatientProfile>("PatientProfileId"); F<PatientMedicalHistoryReview,User>("ReviewedByUserId"); F<PatientMedicalHistoryReview,PatientMedicalHistoryReview>("StateReviewId"); b.Entity<PatientMedicalHistoryReview>().HasIndex(x=>new{x.PatientProfileId,x.ReviewedAt});
        Check<PatientMedicalHistoryReview>("PatientMedicalHistoryReviews","\"StateReviewId\" IS NULL OR \"StateReviewId\" <> \"Id\"");
        F<PatientAllergyEntry,PatientMedicalHistoryReview>("MedicalHistoryReviewId"); F<PatientConditionEntry,PatientMedicalHistoryReview>("MedicalHistoryReviewId"); F<PatientMedicationEntry,PatientMedicalHistoryReview>("MedicalHistoryReviewId");
        Check<PatientAllergyEntry>("PatientAllergyEntries","length(btrim(\"Name\"))>0"); Check<PatientConditionEntry>("PatientConditionEntries","length(btrim(\"ConditionName\"))>0"); Check<PatientMedicationEntry>("PatientMedicationEntries","length(btrim(\"MedicationName\"))>0");
        F<ClinicalEncounter,Appointment>("AppointmentId"); F<ClinicalEncounter,TreatmentEpisode>("TreatmentEpisodeId"); F<ClinicalEncounter,PatientProfile>("PatientProfileId"); F<ClinicalEncounter,DoctorProfile>("DoctorProfileId"); F<ClinicalEncounter,PatientMedicalHistoryReview>("MedicalHistoryReviewId"); b.Entity<ClinicalEncounter>().HasIndex(x=>x.AppointmentId).IsUnique();
        Check<ClinicalEncounter>("ClinicalEncounters","\"Status\" IN ('OPEN','COMPLETED') AND (\"EndedAt\" IS NULL OR \"EndedAt\">=\"StartedAt\")");
        F<SoapNote,ClinicalEncounter>("ClinicalEncounterId"); F<SoapNote,User>("SignedByUserId"); b.Entity<SoapNote>().HasIndex(x=>x.ClinicalEncounterId).IsUnique(); b.Entity<SoapNote>().Property(x=>x.Revision).IsConcurrencyToken();
        Check<SoapNote>("SoapNotes","(\"Status\"='DRAFT' AND \"SignedAt\" IS NULL AND \"SignedByUserId\" IS NULL) OR (\"Status\"='SIGNED' AND \"SignedAt\" IS NOT NULL AND \"SignedByUserId\" IS NOT NULL)");
        F<SoapAddendum,SoapNote>("OriginalEntryId"); F<SoapAddendum,User>("CreatedByUserId");
        F<TreatmentPlan,TreatmentEpisode>("TreatmentEpisodeId"); b.Entity<TreatmentPlan>().HasIndex(x=>x.TreatmentEpisodeId).IsUnique();
        Check<TreatmentPlan>("TreatmentPlans","\"Status\" IN ('DRAFT','PROPOSED','ACCEPTED','IN_PROGRESS','COMPLETED','CANCELLED') AND length(btrim(\"Title\"))>0");
        F<TreatmentPlanItem,TreatmentPlan>("TreatmentPlanId"); b.Entity<TreatmentPlanItem>().Property(x=>x.UnitPrice).HasPrecision(12,2);
        Check<TreatmentPlanItem>("TreatmentPlanItems","\"UnitPrice\">=0 AND \"Quantity\">0 AND length(btrim(\"ProcedureName\"))>0 AND \"Status\" IN ('PROPOSED','ACCEPTED','IN_PROGRESS','COMPLETED','CANCELLED')");
        F<TreatmentFinancialAccount,TreatmentPlan>("TreatmentPlanId"); b.Entity<TreatmentFinancialAccount>().HasIndex(x=>x.TreatmentPlanId).IsUnique(); b.Entity<TreatmentFinancialAccount>().Property(x=>x.AgreedTotal).HasPrecision(12,2); Check<TreatmentFinancialAccount>("TreatmentFinancialAccounts","\"AgreedTotal\">=0");
        F<TreatmentPayment,TreatmentFinancialAccount>("FinancialAccountId"); F<TreatmentPayment,User>("RecordedByUserId"); b.Entity<TreatmentPayment>().HasIndex(x=>new{x.FinancialAccountId,x.IdempotencyKey}).IsUnique(); b.Entity<TreatmentPayment>().Property(x=>x.Amount).HasPrecision(12,2); Check<TreatmentPayment>("TreatmentPayments","\"Amount\">0");
    }
}
