namespace ClinicaRamirez.Api.Domain.ClinicAdministration;

public enum ClinicSiteImageType { HERO }

public sealed class ClinicSiteImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClinicId { get; set; }
    public ClinicSiteImageType ImageType { get; set; } = ClinicSiteImageType.HERO;
    public byte[] ImageData { get; set; } = [];
    public string ContentType { get; set; } = "";
    public string? FileName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public long Size { get; set; }
    public decimal? FocalPointX { get; set; }
    public decimal? FocalPointY { get; set; }
    public string? AltText { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
