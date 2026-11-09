namespace ClinicaRamirez.Api.Domain.ClinicAdministration;

public sealed class ClinicService : IAdministrativeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClinicId { get; set; }
    public Guid? SpecialtyId { get; set; }
    public string Name { get; set; } = "";
    public string ShortDescription { get; set; } = "";
    public string VisualVariant { get; set; } = "TEAL";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
