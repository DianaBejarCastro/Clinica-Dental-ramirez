using System.ComponentModel.DataAnnotations;
namespace ClinicaRamirez.Api.DTOs.ClinicAdministration;
public record ClinicSocialNetworkCreateRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(1000)] string Url,
    [Range(0, int.MaxValue)] int DisplayOrder = 0, bool IsActive = true);
public sealed record ClinicSocialNetworkUpdateRequest(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(1000)] string Url,
    [Range(0, int.MaxValue)] int DisplayOrder = 0, bool IsActive = true)
    : ClinicSocialNetworkCreateRequest(Name, Url, DisplayOrder, IsActive);
public sealed record ClinicSocialNetworkStatusRequest(bool IsActive);
public sealed record ClinicSocialNetworkResponse(Guid Id, string Name, string Url, bool HasLogo,
    string? LogoContentType, string? LogoFileName, long? LogoSize, int DisplayOrder, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt);
public sealed record PublicClinicSocialNetworkResponse(Guid Id, string Name, string Url, bool HasLogo);
