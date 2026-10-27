using ClinicaRamirez.Api.Domain.ClinicAdministration;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;

public static class ClinicAdministrationConfiguration
{
    public static void ConfigureClinicAdministration(this ModelBuilder b)
    {
        b.Entity<Clinic>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.LegalName).HasMaxLength(250);
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.LogoUrl).HasMaxLength(2048);
            e.Property(x => x.LogoData).HasColumnType("bytea");
            e.Property(x => x.LogoContentType).HasMaxLength(50);
            e.Property(x => x.LogoFileName).HasMaxLength(255);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Whatsapp).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.TaxId).HasMaxLength(50);
            e.ToTable("Clinics", t => {
                t.HasCheckConstraint("CK_Clinics_Singleton", "\"Id\" = '00000000-0000-0000-0000-000000000001'::uuid");
                t.HasCheckConstraint("CK_Clinics_Name", "length(btrim(\"Name\")) > 0");
            });
        });
        b.Entity<Center>(e => {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Clinic).WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Address).HasMaxLength(500).IsRequired();
            e.Property(x => x.City).HasMaxLength(100);
            e.Property(x => x.Department).HasMaxLength(100);
            e.Property(x => x.Country).HasMaxLength(100).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Whatsapp).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Latitude).HasPrecision(10, 7);
            e.Property(x => x.Longitude).HasPrecision(10, 7);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.ClinicId).IsUnique().HasDatabaseName("IX_Centers_ActiveMain")
                .HasFilter("\"IsMain\" AND \"IsActive\"");
            e.HasIndex(x => new { x.ClinicId, x.IsActive });
            e.ToTable("Centers", t => {
                t.HasCheckConstraint("CK_Centers_Code", "\"Code\" ~ '^[A-Z][A-Z0-9_]*$'");
                t.HasCheckConstraint("CK_Centers_RequiredText", "length(btrim(\"Name\")) > 0 AND length(btrim(\"Address\")) > 0 AND length(btrim(\"Country\")) > 0");
                t.HasCheckConstraint("CK_Centers_Latitude", "\"Latitude\" IS NULL OR \"Latitude\" BETWEEN -90 AND 90");
                t.HasCheckConstraint("CK_Centers_Longitude", "\"Longitude\" IS NULL OR \"Longitude\" BETWEEN -180 AND 180");
            });
        });
        b.Entity<DoctorProfile>(e => {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithOne().HasForeignKey<DoctorProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId).IsUnique();
            e.Property(x => x.ProfessionalLicense).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.ProfessionalLicense).IsUnique().HasFilter("\"ProfessionalLicense\" IS NOT NULL");
            e.Property(x => x.Bio).HasMaxLength(4000);
            e.HasIndex(x => x.IsActive);
            e.ToTable("DoctorProfiles", t => t.HasCheckConstraint("CK_DoctorProfiles_License", "\"ProfessionalLicense\" IS NULL OR (length(btrim(\"ProfessionalLicense\")) > 0 AND \"ProfessionalLicense\" = upper(btrim(\"ProfessionalLicense\")))"));
        });
        b.Entity<Specialty>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(80).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Name).IsUnique();
            e.ToTable("Specialties", t => {
                t.HasCheckConstraint("CK_Specialties_Code", "\"Code\" ~ '^[A-Z][A-Z0-9_]*$'");
                t.HasCheckConstraint("CK_Specialties_Name", "length(btrim(\"Name\")) > 0");
            });
        });
        b.Entity<DoctorSpecialty>(e => {
            e.HasKey(x => new { x.DoctorId, x.SpecialtyId });
            e.HasOne(x => x.Doctor).WithMany(x => x.DoctorSpecialties).HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Specialty).WithMany(x => x.DoctorSpecialties).HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<DoctorCenter>(e => {
            e.HasKey(x => new { x.DoctorId, x.CenterId });
            e.HasOne(x => x.Doctor).WithMany(x => x.DoctorCenters).HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<DoctorSchedule>(e => {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Center).WithMany().HasForeignKey(x => x.CenterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.DoctorCenter).WithMany().HasForeignKey(x => new { x.DoctorId, x.CenterId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.DoctorId, x.DayOfWeek, x.IsActive });
            e.ToTable("DoctorSchedules", t => {
                t.HasCheckConstraint("CK_DoctorSchedules_Day", "\"DayOfWeek\" BETWEEN 1 AND 7");
                t.HasCheckConstraint("CK_DoctorSchedules_Times", "\"StartTime\" < \"EndTime\"");
                t.HasCheckConstraint("CK_DoctorSchedules_Break", "(NOT \"HasBreak\" AND \"BreakStart\" IS NULL AND \"BreakEnd\" IS NULL) OR (\"HasBreak\" AND \"BreakStart\" IS NOT NULL AND \"BreakEnd\" IS NOT NULL AND \"StartTime\" < \"BreakStart\" AND \"BreakStart\" < \"BreakEnd\" AND \"BreakEnd\" < \"EndTime\")");
            });
        });
    }
}
