using ClinicaRamirez.Api.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;

public static class PatientsConfiguration
{
    public static void ConfigurePatients(this ModelBuilder b)
    {
        b.Entity<PatientProfile>(e => {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithOne().HasForeignKey<PatientProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Ci).HasMaxLength(50);
            e.Property(x => x.Gender).HasMaxLength(50);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Address).HasMaxLength(500);
            e.Property(x => x.EmergencyContactName).HasMaxLength(200);
            e.Property(x => x.EmergencyContactPhone).HasMaxLength(30);
            e.Property(x => x.BloodType).HasMaxLength(10);
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.HasIndex(x => x.UserId).IsUnique().HasFilter("\"UserId\" IS NOT NULL");
            e.HasIndex(x => x.Ci).IsUnique().HasFilter("\"Ci\" IS NOT NULL");
            e.HasIndex(x => x.LastName);
            e.HasIndex(x => x.IsActive);
            e.ToTable("PatientProfiles", t => {
                t.HasCheckConstraint("CK_PatientProfiles_Names", "length(btrim(\"FirstName\")) > 0 AND length(btrim(\"LastName\")) > 0");
                t.HasCheckConstraint("CK_PatientProfiles_Ci", "\"Ci\" IS NULL OR (length(\"Ci\") > 0 AND \"Ci\" = upper(btrim(\"Ci\")))");
            });
        });
    }
}
