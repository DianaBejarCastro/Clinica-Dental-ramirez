using ClinicaRamirez.Api.Domain.ClinicAdministration;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;
public static class ClinicServicesConfiguration
{
    public static void ConfigureClinicServices(this ModelBuilder b)
    {
        b.Entity<ClinicService>(e => {
            e.ToTable("ClinicServices", t => {
                t.HasCheckConstraint("CK_ClinicServices_Order", "\"DisplayOrder\" >= 0");
                t.HasCheckConstraint("CK_ClinicServices_Name", "length(btrim(\"Name\")) > 0");
                t.HasCheckConstraint("CK_ClinicServices_Variant", "\"VisualVariant\" IN ('TEAL','CYAN','NAVY','AQUA','MINT','DARK_TEAL')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.ShortDescription).HasMaxLength(300).IsRequired();
            e.Property(x => x.VisualVariant).HasMaxLength(16).IsRequired();
            e.HasOne<Clinic>().WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Specialty>().WithMany().HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.ClinicId, x.IsActive, x.IsPublic, x.DisplayOrder });
        });
    }
}
