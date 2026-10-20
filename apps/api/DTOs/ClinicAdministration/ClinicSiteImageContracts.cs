using System.ComponentModel.DataAnnotations;

namespace ClinicaRamirez.Api.DTOs.ClinicAdministration;

public sealed class HeroUploadRequest
{
    [Required] public IFormFile File { get; set; } = null!;
    [Range(0, 1)] public decimal? FocalPointX { get; set; }
    [Range(0, 1)] public decimal? FocalPointY { get; set; }
    [MaxLength(300)] public string? AltText { get; set; }
}
public sealed record HeroImageResponse(string ContentType, string? FileName, int Width, int Height, long Size,
    decimal? FocalPointX, decimal? FocalPointY, string? AltText, DateTime UpdatedAt);
public sealed record HeroPublicMetadata(decimal? FocalPointX, decimal? FocalPointY, string AltText, DateTime UpdatedAt);
