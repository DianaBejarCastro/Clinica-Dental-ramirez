using ClinicaRamirez.Api.Domain.ClinicAdministration;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;
public static class ClinicSocialNetworkConfiguration
{
    public static void ConfigureClinicSocialNetworks(this ModelBuilder b)
    {
        b.Entity<ClinicSocialNetwork>(e => {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Clinic).WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            e.Property(x => x.LogoData).HasColumnType("bytea");
            e.Property(x => x.LogoContentType).HasMaxLength(50);
            e.Property(x => x.LogoFileName).HasMaxLength(255);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.ClinicId);
            e.HasIndex(x => new { x.ClinicId, x.IsActive });
            e.HasIndex(x => new { x.ClinicId, x.DisplayOrder });
            e.HasIndex(x => new { x.ClinicId, x.Url }).IsUnique();
            e.ToTable("ClinicSocialNetworks", t => {
                t.HasCheckConstraint("CK_ClinicSocialNetworks_Name", "length(btrim(\"Name\")) > 0 AND \"Name\" = btrim(\"Name\")");
                t.HasCheckConstraint("CK_ClinicSocialNetworks_Url", "\"Url\" ~* '^https?://[^[:space:]]+$'");
                t.HasCheckConstraint("CK_ClinicSocialNetworks_Order", "\"DisplayOrder\" >= 0");
            });
        });
    }
}
