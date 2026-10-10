namespace ClinicaRamirez.Api.Domain.ClinicAdministration;

public sealed class ClinicSocialNetwork : IAdministrativeRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClinicId { get; set; }
    public Clinic Clinic { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public byte[]? LogoData { get; set; }
    public string? LogoContentType { get; set; }
    public string? LogoFileName { get; set; }
    public long? LogoSize { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
