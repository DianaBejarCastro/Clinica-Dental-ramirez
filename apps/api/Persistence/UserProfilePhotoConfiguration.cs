using ClinicaRamirez.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Persistence;
public static class UserProfilePhotoConfiguration
{
    public static void ConfigureUserProfilePhotos(this ModelBuilder b)
    {
        b.Entity<UserProfilePhoto>(e => {
            e.ToTable("UserProfilePhotos", t => {
                t.HasCheckConstraint("CK_UserProfilePhotos_Dimensions", "\"Width\" = 1080 AND \"Height\" = 1350");
                t.HasCheckConstraint("CK_UserProfilePhotos_Type", "\"MimeType\" IN ('image/png','image/jpeg','image/webp')");
                t.HasCheckConstraint("CK_UserProfilePhotos_Size", "\"FileSizeBytes\" > 0 AND \"FileSizeBytes\" <= 5242880 AND octet_length(\"ImageData\") = \"FileSizeBytes\"");
            });
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne<User>().WithOne().HasForeignKey<UserProfilePhoto>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.MimeType).HasMaxLength(32);
        });
    }
}
