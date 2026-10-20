namespace ClinicaRamirez.Api.Domain.DentalCharting;

// No clinical arrays or JSON: every finding, surface and measurement is relational.
public interface IChartExam
{
    Guid Id { get; set; }
    Guid PatientProfileId { get; set; }
    Guid? ClinicalEncounterId { get; set; }
    string DentitionMode { get; set; }
    DateTime ClinicalDate { get; set; }
    string Status { get; set; }
    Guid? PreviousExamId { get; set; }
    Guid CreatedByDoctorProfileId { get; set; }
    Guid? FinalizedByDoctorProfileId { get; set; }
    DateTime? FinalizedAt { get; set; }
    string? GeneralNotes { get; set; }
    int Revision { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}
public sealed class OdontogramExam : IChartExam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientProfileId { get; set; }
    public Guid? ClinicalEncounterId { get; set; }
    public string DentitionMode { get; set; } = "PERMANENT";
    public DateTime ClinicalDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public Guid? PreviousExamId { get; set; }
    public Guid CreatedByDoctorProfileId { get; set; }
    public Guid? FinalizedByDoctorProfileId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public string? GeneralNotes { get; set; }
    public int Revision { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class PeriodontalExam : IChartExam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PatientProfileId { get; set; }
    public Guid? ClinicalEncounterId { get; set; }
    public string DentitionMode { get; set; } = "PERMANENT";
    public DateTime ClinicalDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public Guid? PreviousExamId { get; set; }
    public Guid CreatedByDoctorProfileId { get; set; }
    public Guid? FinalizedByDoctorProfileId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public string? GeneralNotes { get; set; }
    public int Revision { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class DentalTooth
{
    public int Id { get; set; }
    public int FdiCode { get; set; }
    public string DentitionClass { get; set; } = "";
    public int Quadrant { get; set; }
    public string ToothType { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class DentalSurface
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class OdontogramFindingType
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string Category { get; set; } = "";
    public bool AppliesToSurface { get; set; }
    public bool AppliesToWholeTooth { get; set; }
}
public sealed class PeriodontalSite
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public string SurfaceGroup { get; set; } = "";
}
public sealed class OdontogramFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DentalToothId { get; set; }
    public string? Observation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid OdontogramExamId { get; set; }
    public int FindingTypeId { get; set; }
}
public sealed class PeriodontalSiteMeasurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DentalToothId { get; set; }
    public string? Observation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid PeriodontalExamId { get; set; }
    public int PeriodontalSiteId { get; set; }
    public decimal? ProbingDepthMm { get; set; }
    // Positive = apical to CEJ (recession); negative = coronal. CAL = probing depth + margin.
    public decimal? GingivalMarginMm { get; set; }
    public bool BleedingOnProbing { get; set; }
    public bool Plaque { get; set; }
    public bool Suppuration { get; set; }
}
public sealed class PeriodontalToothAssessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DentalToothId { get; set; }
    public string? Observation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid PeriodontalExamId { get; set; }
    public int? MobilityGrade { get; set; }
}
public sealed class PeriodontalFurcationAssessment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DentalToothId { get; set; }
    public string? Observation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid PeriodontalExamId { get; set; }
    public string Location { get; set; } = "";
    public int Grade { get; set; }
}
public sealed class OdontogramFindingSurface
{
    public Guid OdontogramFindingId { get; set; }
    public int DentalSurfaceId { get; set; }
}
public static class ChartingRules
{
    public const int MaxItems = 312, MaxNotes = 4000;
    public const int MaxProbingDepth = 15, MinMargin = -10, MaxMargin = 20, MaxMobility = 3, MaxFurcation = 3;
    public static readonly string[] Dentitions = ["PERMANENT", "PRIMARY", "MIXED"];
    // Grades 0..3: none, incipient, partial, through-and-through involvement.
    public static readonly string[] FurcationLocations = ["BUCCAL", "LINGUAL_PALATAL", "MESIAL", "DISTAL"];
}
