using ClinicaRamirez.Api.Domain.DentalAssistants;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Persistence;
public static class DentalAssistantsConfiguration
{
    public static void ConfigureDentalAssistants(this ModelBuilder b)
    {
        b.Entity<DentalAssistantProfile>(e => {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.User).WithOne().HasForeignKey<DentalAssistantProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => x.IsActive);
        });
    }
}
