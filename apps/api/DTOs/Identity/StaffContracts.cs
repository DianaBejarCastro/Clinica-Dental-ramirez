using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClinicaRamirez.Api.DTOs.Identity;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class StaffCreateRequest
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
    [Required, EmailAddress, MaxLength(254)] public string Email { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; init; } = "";
    [Required, Compare(nameof(Password)), MaxLength(128)] public string ConfirmPassword { get; init; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AddDoctorProfileRequest
{
    [Required, MaxLength(100)] public string ProfessionalLicense { get; init; } = "";
    [Url, MaxLength(1000)] public string? PhotoUrl { get; init; }
    [MaxLength(4000)] public string? Bio { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AddPatientProfileRequest
{
    [Required, MaxLength(200)] public string EmergencyContactName { get; init; } = "";
    [Required, MaxLength(30)] public string EmergencyContactPhone { get; init; } = "";
    [Required, RegularExpression("^(A[+-]|B[+-]|AB[+-]|O[+-]|UNKNOWN)$")] public string BloodType { get; init; } = "";
    [MaxLength(4000)] public string? Notes { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AddDentalAssistantProfileRequest { }
