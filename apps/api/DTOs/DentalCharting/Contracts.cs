using System.ComponentModel.DataAnnotations;
using ClinicaRamirez.Api.Domain.DentalCharting;
namespace ClinicaRamirez.Api.DTOs.DentalCharting;

public sealed record ExamCreateRequest([Required] string DentitionMode, DateTimeOffset ClinicalDate, Guid? ClinicalEncounterId, [MaxLength(4000)] string? GeneralNotes);
public sealed record ExamVersionRequest(DateTimeOffset ClinicalDate, Guid? ClinicalEncounterId);
public sealed record ExamRevisionRequest([Range(0, int.MaxValue)] int Revision);
public sealed record FindingInput(int DentalToothId, int FindingTypeId, [Required, MaxLength(5)] int[] SurfaceIds, [MaxLength(4000)] string? Observation);
public sealed record OdontogramUpdateRequest(int Revision, [Required] string DentitionMode, [MaxLength(4000)] string? GeneralNotes, [Required, MaxLength(312)] FindingInput[] Findings);
public sealed record MeasurementInput(int DentalToothId, int PeriodontalSiteId,
    [Range(0, ChartingRules.MaxProbingDepth)] decimal? ProbingDepthMm,
    [Range(ChartingRules.MinMargin, ChartingRules.MaxMargin)] decimal? GingivalMarginMm,
    bool BleedingOnProbing, bool Plaque, bool Suppuration, [MaxLength(4000)] string? Observation);
public sealed record ToothAssessmentInput(int DentalToothId, [Range(0, ChartingRules.MaxMobility)] int? MobilityGrade, [MaxLength(4000)] string? Observation);
public sealed record FurcationInput(int DentalToothId, [Required] string Location, [Range(0, ChartingRules.MaxFurcation)] int Grade, [MaxLength(4000)] string? Observation);
public sealed record PeriodontalUpdateRequest(int Revision, [Required] string DentitionMode, [MaxLength(4000)] string? GeneralNotes,
    [Required, MaxLength(312)] MeasurementInput[] Measurements, [Required, MaxLength(52)] ToothAssessmentInput[] ToothAssessments, [Required, MaxLength(208)] FurcationInput[] Furcations);
public sealed record ExamSummary(Guid Id, Guid PatientProfileId, Guid? ClinicalEncounterId, DateTime ClinicalDate, string DentitionMode,
    string Status, string CreatedByDoctorName, Guid? PreviousExamId, bool IsLatest, int Revision, string? GeneralNotes, DateTime? FinalizedAt, Guid? FinalizedByDoctorProfileId);
public sealed record FindingDto(Guid Id, int DentalToothId, int FindingTypeId, int[] SurfaceIds, string? Observation);
public sealed record OdontogramDetail(ExamSummary Exam, FindingDto[] Findings);
public sealed record MeasurementDto(Guid Id, int DentalToothId, int PeriodontalSiteId, decimal? ProbingDepthMm, decimal? GingivalMarginMm,
    decimal? ClinicalAttachmentLevelMm, bool BleedingOnProbing, bool Plaque, bool Suppuration, string? Observation);
public sealed record PeriodontalDetail(ExamSummary Exam, MeasurementDto[] Measurements, ToothAssessmentInput[] ToothAssessments, FurcationInput[] Furcations);
public sealed record DentalToothDto(int Id, int FdiCode, string DentitionClass, int Quadrant, string ToothType, int DisplayOrder, bool IsActive);
public sealed record DentalSurfaceDto(int Id, string Code, string Name, int DisplayOrder, bool IsActive);
public sealed record FindingTypeDto(int Id, string Code, string Name, string Category, bool AppliesToSurface, bool AppliesToWholeTooth, int DisplayOrder, bool IsActive);
public sealed record PeriodontalSiteDto(int Id, string Code, string Name, string SurfaceGroup, int DisplayOrder, bool IsActive);
