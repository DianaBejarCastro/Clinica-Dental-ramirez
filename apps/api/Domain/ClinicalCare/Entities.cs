namespace ClinicaRamirez.Api.Domain.ClinicalCare;

public interface IClinicalRecord { }
public interface IClinicalImmutable : IClinicalRecord { }
public sealed class TreatmentEpisode : IClinicalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientProfileId { get; set; }
    public Guid PrimaryDoctorProfileId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "OPEN";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class TreatmentAnamnesis : IClinicalImmutable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TreatmentEpisodeId { get; set; }
    public string ChiefComplaint { get; set; } = "";
    public string PainSince { get; set; } = "";
    public bool? IsPainConstant { get; set; }
    public bool? ColdSensitivity { get; set; }
    public bool? HeatSensitivity { get; set; }
    public bool? PainWhenChewing { get; set; }
    public bool? WakesFromPain { get; set; }
    public string? PainMedicationTaken { get; set; }
    public int? PainIntensity { get; set; }
    public string? InitialObservations { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public Guid RecordedByUserId { get; set; }
}
public sealed class PatientMedicalHistoryReview : IClinicalImmutable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientProfileId { get; set; }
    // A no-change review points directly to the immutable snapshot, never to another reference.
    public Guid? StateReviewId { get; set; }
    public bool NoKnownAllergies { get; set; }
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
    public Guid ReviewedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class PatientAllergyEntry : IClinicalImmutable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedicalHistoryReviewId { get; set; }
    public string Name { get; set; } = "";
    public string? Reaction { get; set; }
    public string? Severity { get; set; }
    public bool IsCurrentlyActive { get; set; } = true;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
public sealed class PatientConditionEntry : IClinicalImmutable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedicalHistoryReviewId { get; set; }
    public string ConditionName { get; set; } = "";
    public string? Details { get; set; }
    public bool IsCurrentlyActive { get; set; } = true;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
public sealed class PatientMedicationEntry : IClinicalImmutable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MedicalHistoryReviewId { get; set; }
    public string MedicationName { get; set; } = "";
    public string? Dose { get; set; }
    public string? Frequency { get; set; }
    public string? Reason { get; set; }
    public bool IsCurrentlyActive { get; set; } = true;
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
public sealed class ClinicalEncounter : IClinicalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AppointmentId { get; set; }
    public Guid TreatmentEpisodeId { get; set; }
    public Guid PatientProfileId { get; set; }
    public Guid DoctorProfileId { get; set; }
    public Guid MedicalHistoryReviewId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public string Status { get; set; } = "OPEN";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class SoapNote : IClinicalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClinicalEncounterId { get; set; }
    public string Subjective { get; set; } = "";
    public string Objective { get; set; } = "";
    public string Assessment { get; set; } = "";
    public string Plan { get; set; } = "";
    public string Status { get; set; } = "DRAFT";
    public long Revision { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SignedAt { get; set; }
    public Guid? SignedByUserId { get; set; }
}
public sealed class SoapAddendum : IClinicalImmutable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OriginalEntryId { get; set; }
    public string Reason { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
}
public sealed class TreatmentPlan : IClinicalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TreatmentEpisodeId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "DRAFT";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }
}
public sealed class TreatmentPlanItem : IClinicalRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TreatmentPlanId { get; set; }
    public string ProcedureName { get; set; } = "";
    public string? Description { get; set; }
    public int? ToothNumber { get; set; }
    public string? ToothSurface { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public string Status { get; set; } = "PROPOSED";
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class TreatmentFinancialAccount : IClinicalImmutable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TreatmentPlanId { get; set; }
    public decimal AgreedTotal { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class TreatmentPayment : IClinicalImmutable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FinancialAccountId { get; set; }
    public Guid IdempotencyKey { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    public string? PaymentMethod { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public Guid RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
