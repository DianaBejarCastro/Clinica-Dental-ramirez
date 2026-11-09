namespace ClinicaRamirez.Api.Domain.ClinicAdministration;
public sealed class ClinicFeaturedDoctor : IAdministrativeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClinicId { get; set; }
    public Guid DoctorProfileId { get; set; }
    public DoctorProfile Doctor { get; set; } = null!;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
