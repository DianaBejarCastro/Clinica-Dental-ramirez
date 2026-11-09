using System.ComponentModel.DataAnnotations;
namespace ClinicaRamirez.Api.DTOs.ClinicAdministration;
public sealed record ClinicServiceRequest(
    [Required, MaxLength(120)] string Name,
    [MaxLength(300)] string? ShortDescription,
    [Required, RegularExpression("^(TEAL|CYAN|NAVY|AQUA|MINT|DARK_TEAL)$")] string VisualVariant,
    [Range(0, int.MaxValue)] int DisplayOrder,
    bool IsActive, bool IsPublic, Guid? SpecialtyId);
public sealed record ClinicServiceResponse(Guid Id, string Name, string ShortDescription, string VisualVariant,
    int DisplayOrder, bool IsActive, bool IsPublic, Guid? SpecialtyId);
public sealed record PublicClinicServiceResponse(Guid Id, string Name, string ShortDescription, string VisualVariant);
public sealed record ClinicServiceStatusRequest(bool IsActive);
