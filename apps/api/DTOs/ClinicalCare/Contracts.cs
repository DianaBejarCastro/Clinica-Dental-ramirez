using System.ComponentModel.DataAnnotations;
namespace ClinicaRamirez.Api.DTOs.ClinicalCare;
public sealed record AnamnesisInput(
    [Required,MaxLength(8000)] string ChiefComplaint, [Required,MaxLength(8000)] string PainSince,
    bool? IsPainConstant, bool? ColdSensitivity, bool? HeatSensitivity, bool? PainWhenChewing, bool? WakesFromPain,
    [MaxLength(8000)] string? PainMedicationTaken, [Range(0,10)] int? PainIntensity, [MaxLength(8000)] string? InitialObservations);
public sealed record AllergyInput([Required,MaxLength(200)] string Name,[MaxLength(8000)] string? Reaction,[MaxLength(8000)] string? Severity,bool IsCurrentlyActive=true);
public sealed record ConditionInput([Required,MaxLength(200)] string ConditionName,[MaxLength(8000)] string? Details,bool IsCurrentlyActive=true);
public sealed record MedicationInput([Required,MaxLength(200)] string MedicationName,[MaxLength(8000)] string? Dose,[MaxLength(8000)] string? Frequency,[MaxLength(8000)] string? Reason,bool IsCurrentlyActive=true);
public sealed record ReviewInput(bool NoChanges, Guid? ExpectedReviewId, bool NoKnownAllergies,
    [MaxLength(8000)] string? Notes, [Required,MaxLength(100)] AllergyInput[] Allergies,
    [Required,MaxLength(100)] ConditionInput[] Conditions,[Required,MaxLength(100)] MedicationInput[] Medications);
public sealed record StartEncounterRequest(Guid? TreatmentEpisodeId,[MaxLength(200)] string? Title,AnamnesisInput? Anamnesis,[Required] ReviewInput Review);
public sealed record SoapInput([MaxLength(8000)] string Subjective,[MaxLength(8000)] string Objective,[MaxLength(8000)] string Assessment,[MaxLength(8000)] string Plan,long Revision,bool Sign=false);
public sealed record AddendumInput([Required,MaxLength(8000)] string Reason,[Required,MaxLength(8000)] string Content);
public sealed record PlanInput([Required,MaxLength(200)] string Title);
public sealed record ProcedureInput([Required,MaxLength(200)] string ProcedureName,[MaxLength(8000)] string? Description,int? ToothNumber,[MaxLength(8000)] string? ToothSurface,decimal UnitPrice,[Range(1,10000)] int Quantity=1,int DisplayOrder=0);
public sealed record ClinicalStatusInput([Required] string Status);
public sealed record PaymentInput(Guid IdempotencyKey,decimal Amount,[MaxLength(8000)] string? PaymentMethod,[MaxLength(8000)] string? Reference,[MaxLength(8000)] string? Notes);
public sealed record ClinicalPatient(Guid Id,string FirstName,string LastName,int? Age,string? Gender,string? Ci,string? Phone,string? Email,string? BloodType = null);
public sealed record ClinicalAppointment(Guid Id,DateOnly Date,TimeOnly StartTime,TimeOnly EndTime,string? Reason,string Center,string Status);
public sealed record EpisodeDto(Guid Id,string Title,string Status,DateTime StartedAt,AnamnesisInput? Anamnesis,DateTime? AnamnesisRecordedAt);
public sealed record ReviewDto(Guid Id,Guid? StateReviewId,DateTime ReviewedAt,bool NoKnownAllergies,string? Notes,AllergyInput[] Allergies,ConditionInput[] Conditions,MedicationInput[] Medications);
public sealed record SoapDto(Guid Id,string Subjective,string Objective,string Assessment,string Plan,string Status,long Revision,DateTime? SignedAt,Guid? SignedByUserId);
public sealed record ProcedureDto(Guid Id,string ProcedureName,string? Description,int? ToothNumber,string? ToothSurface,decimal UnitPrice,int Quantity,string Status,int DisplayOrder);
public sealed record PaymentDto(Guid Id,decimal Amount,DateTime PaidAt,string? PaymentMethod,string? Reference);
public sealed record FinanceDto(Guid Id,decimal AgreedTotal,decimal PaidTotal,decimal PendingBalance,string PaymentStatus,PaymentDto[] Payments);
public sealed record PlanDto(Guid Id,string Title,string Status,decimal Total,ProcedureDto[] Items,FinanceDto? Finance);
public sealed record TimelineDto(Guid Id,string Type,DateTime At,string Title,string? Content);
public sealed record EncounterDto(Guid Id,Guid TreatmentEpisodeId,string Status,DateTime StartedAt,SoapDto Soap);
public sealed record ClinicalContext(ClinicalPatient Patient,ClinicalAppointment Appointment,EncounterDto? Encounter,EpisodeDto[] Episodes,ReviewDto? LatestReview,PlanDto? Plan);
public sealed record ClinicalHistory(ReviewDto[] MedicalReviews,EpisodeDto[] Episodes,TimelineDto[] Events);
