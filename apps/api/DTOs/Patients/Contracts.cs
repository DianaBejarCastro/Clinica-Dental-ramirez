using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ClinicaRamirez.Api.DTOs;

namespace ClinicaRamirez.Api.DTOs.Patients;

public abstract record PatientClinicalRequest
{
    [Required, MaxLength(100)] public string FirstName { get; init; } = "";
    [Required, MaxLength(100)] public string LastName { get; init; } = "";
    [Required, MaxLength(50)] public string? DocumentNumber { get; init; }
    public string? DocumentType { get; init; }
    [MaxLength(2)] public string? DocumentCountryCode { get; init; }
    [Required, LocalBirthDate] public DateOnly? BirthDate { get; init; }
    [MaxLength(50)] public string? Gender { get; init; }
    [Required, MaxLength(30)] public string? Phone { get; init; }
    [MaxLength(500)] public string? Address { get; init; }
    [Required, MaxLength(200)] public string? EmergencyContactName { get; init; }
    [Required, MaxLength(30)] public string? EmergencyContactPhone { get; init; }
    [Required, RegularExpression("^(A[+-]|B[+-]|AB[+-]|O[+-]|UNKNOWN)$")] public string? BloodType { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record PatientUpdateRequest : PatientClinicalRequest
{
    [MaxLength(4000)] public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatientCreateRequest : PatientUpdateRequest
{
    public PatientAccountCreateRequest? Account { get; init; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatientAccountCreateRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; init; } = "";
    [Required, MaxLength(128), Compare(nameof(Password))] public string ConfirmPassword { get; init; } = "";
}
public sealed record PatientStatusRequest(bool IsActive);
public sealed record PatientResponse(Guid Id, Guid UserId, string FirstName, string LastName,
    string? DocumentType, string? DocumentNumber, string? DocumentCountryCode, DateOnly? BirthDate, string? Gender, string? Phone, string? Email, string? Address,
    string? EmergencyContactName, string? EmergencyContactPhone, string? BloodType, string? Notes,
    bool IsActive, DateTime CreatedAt, DateTime UpdatedAt, bool CanLogin);
public sealed record PatientListResponse(PatientResponse[] Items, int Total, int Page, int PageSize);
public sealed record PatientSearchResult(Guid PatientId, Guid UserId, string FirstName, string LastName, string? DocumentType, string? DocumentNumber, string? DocumentCountryCode, string? Phone, string? Email);
