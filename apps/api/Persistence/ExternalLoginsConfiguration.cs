using ClinicaRamirez.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Persistence;

public static class ExternalLoginsConfiguration
{
    public static void ConfigureExternalLogins(this ModelBuilder b)
    {
        b.Entity<UserExternalLogin>(e => {
            e.HasKey(x => x.Id);
            e.Property(x => x.Provider).HasMaxLength(30).IsRequired();
            e.Property(x => x.ProviderSubject).HasMaxLength(255).IsRequired();
            e.Property(x => x.ProviderEmail).HasMaxLength(254).IsRequired();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Provider, x.ProviderSubject }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.Provider }).IsUnique();
            e.ToTable("UserExternalLogins", t => {
                t.HasCheckConstraint("CK_UserExternalLogins_Provider", "\"Provider\" = 'GOOGLE'");
                t.HasCheckConstraint("CK_UserExternalLogins_Subject", "length(btrim(\"ProviderSubject\")) > 0");
            });
        });
    }
}
