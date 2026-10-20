using ClinicaRamirez.Api.Domain.ClinicAdministration;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;

public static class ClinicSiteImagesConfiguration
{
    public static void ConfigureClinicSiteImages(this ModelBuilder b) => b.Entity<ClinicSiteImage>(e => {
        e.HasKey(x => x.Id);
        e.HasOne<Clinic>().WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
        e.Property(x => x.ImageType).HasConversion<string>().HasMaxLength(20);
        e.HasIndex(x => new { x.ClinicId, x.ImageType }).IsUnique();
        e.Property(x => x.ImageData).HasColumnType("bytea").IsRequired();
        e.Property(x => x.ContentType).HasMaxLength(40).IsRequired();
        e.Property(x => x.FileName).HasMaxLength(255);
        e.Property(x => x.AltText).HasMaxLength(300);
        e.Property(x => x.FocalPointX).HasPrecision(6, 5);
        e.Property(x => x.FocalPointY).HasPrecision(6, 5);
        e.ToTable("ClinicSiteImages", t => {
            t.HasCheckConstraint("CK_ClinicSiteImages_Hero", "\"ImageType\" = 'HERO' AND \"Width\" = 1916 AND \"Height\" = 821");
            t.HasCheckConstraint("CK_ClinicSiteImages_Content", "\"ContentType\" IN ('image/png','image/jpeg','image/webp') AND \"Size\" > 0 AND \"Size\" <= 10485760 AND octet_length(\"ImageData\") = \"Size\"");
            t.HasCheckConstraint("CK_ClinicSiteImages_FocalPoint", "(\"FocalPointX\" IS NULL OR \"FocalPointX\" BETWEEN 0 AND 1) AND (\"FocalPointY\" IS NULL OR \"FocalPointY\" BETWEEN 0 AND 1)");
        });
    });
}
