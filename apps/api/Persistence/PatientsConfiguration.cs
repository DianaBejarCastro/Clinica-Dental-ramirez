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
            e.Property(x => x.EmergencyContactName).HasMaxLength(200).IsRequired();
            e.Property(x => x.EmergencyContactPhone).HasMaxLength(30).IsRequired();
            e.Property(x => x.BloodType).HasMaxLength(10).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(4000);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => x.IsActive);
            e.ToTable("PatientProfiles", t => {
                t.HasCheckConstraint("CK_PatientProfiles_RequiredContact", "length(btrim(\"EmergencyContactName\")) > 0 AND length(btrim(\"EmergencyContactPhone\")) > 0");
                t.HasCheckConstraint("CK_PatientProfiles_BloodType", "\"BloodType\" IN ('A+','A-','B+','B-','AB+','AB-','O+','O-','UNKNOWN')");
            });
        });
    }
}
