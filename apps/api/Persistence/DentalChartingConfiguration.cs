using ClinicaRamirez.Api.Domain.DentalCharting;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.ClinicalCare;
using ClinicaRamirez.Api.Domain.Patients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace ClinicaRamirez.Api.Persistence;

public static class DentalChartingConfiguration
{
    private static void Exam<T>(ModelBuilder b, string table) where T : class, IChartExam {
        var e=b.Entity<T>(); e.HasKey(x=>x.Id); e.Property(x=>x.Status).HasMaxLength(20); e.Property(x=>x.DentitionMode).HasMaxLength(20);
        e.Property(x=>x.GeneralNotes).HasMaxLength(ChartingRules.MaxNotes); e.Property(x=>x.Revision).IsConcurrencyToken();
        e.HasOne<PatientProfile>().WithMany().HasForeignKey(x=>x.PatientProfileId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ClinicalEncounter>().WithMany().HasForeignKey(x=>x.ClinicalEncounterId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<DoctorProfile>().WithMany().HasForeignKey(x=>x.CreatedByDoctorProfileId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<DoctorProfile>().WithMany().HasForeignKey(x=>x.FinalizedByDoctorProfileId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<T>().WithMany().HasForeignKey(x=>x.PreviousExamId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x=>new{x.PatientProfileId,x.ClinicalDate});
        // One linear successor and one open draft per patient; serialized service writes return the existing draft on retries.
        e.HasIndex(x=>x.PreviousExamId).IsUnique().HasFilter("\"PreviousExamId\" IS NOT NULL");
        e.HasIndex(x=>x.PatientProfileId).IsUnique().HasFilter("\"Status\" = 'DRAFT'");
        e.ToTable(table,t=>{
            t.HasCheckConstraint("CK_"+table+"_State", "\"Status\" IN ('DRAFT','FINALIZED') AND \"DentitionMode\" IN ('PERMANENT','PRIMARY','MIXED') AND \"Revision\" >= 0");
            t.HasCheckConstraint("CK_"+table+"_Previous", "\"PreviousExamId\" IS NULL OR \"PreviousExamId\" <> \"Id\"");
            t.HasCheckConstraint("CK_"+table+"_Finalized", "(\"Status\"='DRAFT' AND \"FinalizedAt\" IS NULL AND \"FinalizedByDoctorProfileId\" IS NULL) OR (\"Status\"='FINALIZED' AND \"FinalizedAt\" IS NOT NULL AND \"FinalizedByDoctorProfileId\" IS NOT NULL)");
        });
    }
    public static void ConfigureDentalCharting(this ModelBuilder b) {
        b.Entity<DentalTooth>(e=>{e.ToTable("DentalTeeth",t=>t.HasCheckConstraint("CK_DentalTeeth_Fdi", "\"FdiCode\" = \"Quadrant\"*10 + \"FdiCode\"%10 AND ((\"DentitionClass\"='PERMANENT' AND \"Quadrant\" BETWEEN 1 AND 4 AND \"FdiCode\"%10 BETWEEN 1 AND 8) OR (\"DentitionClass\"='PRIMARY' AND \"Quadrant\" BETWEEN 5 AND 8 AND \"FdiCode\"%10 BETWEEN 1 AND 5)) AND \"ToothType\" IN ('INCISOR','CANINE','PREMOLAR','MOLAR')")); e.HasKey(x=>x.Id); e.Property(x=>x.Id).ValueGeneratedNever();e.HasIndex(x=>x.FdiCode).IsUnique(); e.Property(x=>x.DentitionClass).HasMaxLength(20); e.Property(x=>x.ToothType).HasMaxLength(20);e.HasData(DentalCatalogSeed.Teeth());});
        b.Entity<DentalSurface>(e=>{e.ToTable("DentalSurfaces");e.HasKey(x=>x.Id);e.Property(x=>x.Id).ValueGeneratedNever();e.Property(x=>x.Code).HasMaxLength(60);e.Property(x=>x.Name).HasMaxLength(150);e.HasIndex(x=>x.Code).IsUnique();e.HasData(DentalCatalogSeed.Surfaces());});
        b.Entity<OdontogramFindingType>(e=>{e.ToTable("OdontogramFindingTypes");e.HasKey(x=>x.Id);e.Property(x=>x.Id).ValueGeneratedNever();e.Property(x=>x.Code).HasMaxLength(60);e.Property(x=>x.Name).HasMaxLength(150);e.HasIndex(x=>x.Code).IsUnique();e.Property(x=>x.Category).HasMaxLength(30);e.ToTable("OdontogramFindingTypes",t=>t.HasCheckConstraint("CK_FindingType_Scope","\"AppliesToSurface\" OR \"AppliesToWholeTooth\""));e.HasData(DentalCatalogSeed.Findings());});
        b.Entity<PeriodontalSite>(e=>{e.ToTable("PeriodontalSites");e.HasKey(x=>x.Id);e.Property(x=>x.Id).ValueGeneratedNever();e.Property(x=>x.Code).HasMaxLength(60);e.Property(x=>x.Name).HasMaxLength(150);e.HasIndex(x=>x.Code).IsUnique();e.Property(x=>x.SurfaceGroup).HasMaxLength(30);e.HasData(DentalCatalogSeed.Sites());});
        Exam<OdontogramExam>(b,"OdontogramExams"); Exam<PeriodontalExam>(b,"PeriodontalExams");
        b.Entity<OdontogramFinding>(e=>{e.ToTable("OdontogramFindings");e.HasKey(x=>x.Id);e.Property(x=>x.Observation).HasMaxLength(ChartingRules.MaxNotes);
            e.HasOne<OdontogramExam>().WithMany().HasForeignKey(x=>x.OdontogramExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DentalTooth>().WithMany().HasForeignKey(x=>x.DentalToothId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OdontogramFindingType>().WithMany().HasForeignKey(x=>x.FindingTypeId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<OdontogramFindingSurface>(e=>{e.ToTable("OdontogramFindingSurfaces");e.HasKey(x=>new{x.OdontogramFindingId,x.DentalSurfaceId});
            e.HasOne<OdontogramFinding>().WithMany().HasForeignKey(x=>x.OdontogramFindingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DentalSurface>().WithMany().HasForeignKey(x=>x.DentalSurfaceId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<PeriodontalSiteMeasurement>(e=>{e.ToTable("PeriodontalSiteMeasurements");e.HasKey(x=>x.Id);e.Property(x=>x.Observation).HasMaxLength(ChartingRules.MaxNotes);
            e.HasOne<PeriodontalExam>().WithMany().HasForeignKey(x=>x.PeriodontalExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DentalTooth>().WithMany().HasForeignKey(x=>x.DentalToothId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<PeriodontalSite>().WithMany().HasForeignKey(x=>x.PeriodontalSiteId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x=>new{x.PeriodontalExamId,x.DentalToothId,x.PeriodontalSiteId}).IsUnique();
            e.Property(x=>x.ProbingDepthMm).HasPrecision(4,1);e.Property(x=>x.GingivalMarginMm).HasPrecision(4,1);
            e.Property(x=>x.BleedingOnProbing).HasDefaultValue(false);e.Property(x=>x.Plaque).HasDefaultValue(false);e.Property(x=>x.Suppuration).HasDefaultValue(false);
            e.ToTable("PeriodontalSiteMeasurements",t=>t.HasCheckConstraint("CK_PeriodontalMeasurement_Range",$"(\"ProbingDepthMm\" IS NULL OR \"ProbingDepthMm\" BETWEEN 0 AND {ChartingRules.MaxProbingDepth}) AND (\"GingivalMarginMm\" IS NULL OR \"GingivalMarginMm\" BETWEEN {ChartingRules.MinMargin} AND {ChartingRules.MaxMargin})"));
        });
        b.Entity<PeriodontalToothAssessment>(e=>{e.ToTable("PeriodontalToothAssessments");e.HasKey(x=>x.Id);e.Property(x=>x.Observation).HasMaxLength(ChartingRules.MaxNotes);
            e.HasOne<PeriodontalExam>().WithMany().HasForeignKey(x=>x.PeriodontalExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DentalTooth>().WithMany().HasForeignKey(x=>x.DentalToothId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x=>new{x.PeriodontalExamId,x.DentalToothId}).IsUnique();
            e.ToTable("PeriodontalToothAssessments",t=>t.HasCheckConstraint("CK_PeriodontalMobility_Range",$"\"MobilityGrade\" IS NULL OR \"MobilityGrade\" BETWEEN 0 AND {ChartingRules.MaxMobility}"));
        });
        b.Entity<PeriodontalFurcationAssessment>(e=>{e.ToTable("PeriodontalFurcationAssessments");e.HasKey(x=>x.Id);e.Property(x=>x.Observation).HasMaxLength(ChartingRules.MaxNotes);
            e.HasOne<PeriodontalExam>().WithMany().HasForeignKey(x=>x.PeriodontalExamId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DentalTooth>().WithMany().HasForeignKey(x=>x.DentalToothId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x=>x.Location).HasMaxLength(30);e.HasIndex(x=>new{x.PeriodontalExamId,x.DentalToothId,x.Location}).IsUnique();
            e.ToTable("PeriodontalFurcationAssessments",t=>t.HasCheckConstraint("CK_PeriodontalFurcation_Range",$"\"Grade\" BETWEEN 0 AND {ChartingRules.MaxFurcation} AND \"Location\" IN ('BUCCAL','LINGUAL_PALATAL','MESIAL','DISTAL')"));
        });
    }
}
