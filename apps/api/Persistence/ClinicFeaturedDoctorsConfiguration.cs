using ClinicaRamirez.Api.Domain.ClinicAdministration;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Persistence;
public static class ClinicFeaturedDoctorsConfiguration
{
    public static void ConfigureClinicFeaturedDoctors(this ModelBuilder b) => b.Entity<ClinicFeaturedDoctor>(e => {
        e.ToTable("ClinicFeaturedDoctors", t => t.HasCheckConstraint("CK_ClinicFeaturedDoctors_Order", "\"DisplayOrder\" >= 0"));
        e.HasKey(x => x.Id);
        e.HasOne<Clinic>().WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorProfileId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.ClinicId, x.DoctorProfileId }).IsUnique();
    });
}
