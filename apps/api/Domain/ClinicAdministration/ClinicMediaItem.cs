namespace ClinicaRamirez.Api.Domain.ClinicAdministration;
public enum ClinicMediaType { IMAGE, VIDEO_UPLOAD, TIKTOK }
public sealed class ClinicMediaItem {
 public Guid Id { get; set; } = Guid.NewGuid();
 public Guid ClinicId { get; set; }
 public ClinicMediaType MediaType { get; set; }
 public string? Title { get; set; }
 public string? Caption { get; set; }
 public string? AltText { get; set; }
 public int SortOrder { get; set; }
 public bool IsActive { get; set; } = true;
 public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
 public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
 public Guid? LegacyImageId { get; set; }
 public ClinicSiteImage? LegacyImage { get; set; }
 public ClinicMediaAsset? Asset { get; set; }
 public ClinicMediaExternalSource? External { get; set; }
}
public sealed class ClinicMediaAsset {
 public Guid MediaItemId { get; set; }
 public ClinicMediaItem Item { get; set; } = null!;
 public byte[]? ImageData { get; set; }
 public byte[]? ThumbnailData { get; set; }
 public string? StorageKey { get; set; }
 public string MimeType { get; set; } = "";
 public string OriginalFileName { get; set; } = "";
 public long FileSizeBytes { get; set; }
 public int? Width { get; set; }
 public int? Height { get; set; }
}
public sealed class ClinicMediaExternalSource {
 public Guid MediaItemId { get; set; }
 public ClinicMediaItem Item { get; set; } = null!;
 public string ExternalUrl { get; set; } = "";
 public string ExternalId { get; set; } = "";
}
