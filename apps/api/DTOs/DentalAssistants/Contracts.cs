using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClinicaRamirez.Api.DTOs.DentalAssistants;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class DentalAssistantUpdateRequest
{
    [Required, MaxLength(100)] public string FirstName { get; init; } = "";
    [Required, MaxLength(100)] public string LastName { get; init; } = "";
    [Required, MaxLength(50)] public string DocumentNumber { get; init; } = "";
    public string? DocumentType { get; init; }
    [MaxLength(2)] public string? DocumentCountryCode { get; init; }
    [Required, LocalBirthDate] public DateOnly? BirthDate { get; init; }
    [MaxLength(50)] public string? Gender { get; init; }
    [Required, MaxLength(30)] public string Phone { get; init; } = "";
    [MaxLength(500)] public string? Address { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class DentalAssistantCreateRequest : DentalAssistantUpdateRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; init; } = "";
    [Required, MaxLength(128), Compare(nameof(Password))] public string ConfirmPassword { get; init; } = "";
}
public sealed record DentalAssistantResponse(Guid Id, Guid UserId, string FirstName, string LastName,
    string? DocumentType, string? DocumentNumber, string? DocumentCountryCode, DateOnly? BirthDate, string? Gender, string? Phone, string? Address, string? Email,
    bool IsActive, bool UserIsActive, bool CanLogin, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record DentalAssistantListResponse(DentalAssistantResponse[] Items, int Total, int Page, int PageSize);
