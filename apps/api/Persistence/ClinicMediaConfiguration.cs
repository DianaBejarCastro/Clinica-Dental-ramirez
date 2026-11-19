using ClinicaRamirez.Api.Domain.ClinicAdministration;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Persistence;
public static class ClinicMediaConfiguration {
 public static void ConfigureClinicMedia(this ModelBuilder b) {
  b.Entity<ClinicMediaItem>(e => {
   e.HasKey(x=>x.Id); e.Property(x=>x.MediaType).HasConversion<string>().HasMaxLength(20);
   e.Property(x=>x.Title).HasMaxLength(200);e.Property(x=>x.Caption).HasMaxLength(1000);e.Property(x=>x.AltText).HasMaxLength(300);
   e.HasOne<Clinic>().WithMany().HasForeignKey(x=>x.ClinicId).OnDelete(DeleteBehavior.Restrict);
   e.HasOne(x=>x.LegacyImage).WithMany().HasForeignKey(x=>x.LegacyImageId).OnDelete(DeleteBehavior.Restrict);
   e.HasIndex(x=>x.LegacyImageId).IsUnique();
   e.HasIndex(x=>new{x.ClinicId,x.IsActive,x.SortOrder});
   e.ToTable("ClinicMediaItems",t=>{t.HasCheckConstraint("CK_ClinicMedia_Type","\"MediaType\" IN ('IMAGE','VIDEO_UPLOAD','TIKTOK')");t.HasCheckConstraint("CK_ClinicMedia_Order","\"SortOrder\" >= 0");});
  });
  b.Entity<ClinicMediaAsset>(e=>{
   e.HasKey(x=>x.MediaItemId);e.HasOne(x=>x.Item).WithOne(x=>x.Asset).HasForeignKey<ClinicMediaAsset>(x=>x.MediaItemId).OnDelete(DeleteBehavior.Restrict);
   e.Property(x=>x.StorageKey).HasMaxLength(100);e.Property(x=>x.MimeType).HasMaxLength(40);e.Property(x=>x.OriginalFileName).HasMaxLength(255);
   e.ToTable("ClinicMediaAssets",t=>t.HasCheckConstraint("CK_MediaAsset_Storage","(\"ImageData\" IS NOT NULL AND \"StorageKey\" IS NULL AND \"FileSizeBytes\" BETWEEN 1 AND 10485760) OR (\"ImageData\" IS NULL AND \"StorageKey\" IS NOT NULL AND \"FileSizeBytes\" BETWEEN 1 AND 52428800)"));
  });
  b.Entity<ClinicMediaExternalSource>(e=>{e.HasKey(x=>x.MediaItemId);e.HasOne(x=>x.Item).WithOne(x=>x.External).HasForeignKey<ClinicMediaExternalSource>(x=>x.MediaItemId).OnDelete(DeleteBehavior.Restrict);e.Property(x=>x.ExternalUrl).HasMaxLength(500);e.Property(x=>x.ExternalId).HasMaxLength(30);e.ToTable("ClinicMediaExternalSources");});
 }
}
