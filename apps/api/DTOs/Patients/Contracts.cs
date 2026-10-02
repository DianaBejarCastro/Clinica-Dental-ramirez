using System.ComponentModel.DataAnnotations;

namespace ClinicaRamirez.Api.DTOs.Patients;

public class PatientUpdateRequest
{
    public Guid? UserId { get; init; }
    [Required, MaxLength(100)] public string FirstName { get; init; } = "";
    [Required, MaxLength(100)] public string LastName { get; init; } = "";
    [MaxLength(50)] public string? Ci { get; init; }
    public DateOnly? BirthDate { get; init; }
    [MaxLength(50)] public string? Gender { get; init; }
    [MaxLength(30)] public string? Phone { get; init; }
    [MaxLength(254), EmailAddress] public string? Email { get; init; }
    [MaxLength(500)] public string? Address { get; init; }
    [MaxLength(200)] public string? EmergencyContactName { get; init; }
    [MaxLength(30)] public string? EmergencyContactPhone { get; init; }
    [MaxLength(10)] public string? BloodType { get; init; }
    [MaxLength(4000)] public string? Notes { get; init; }
    public bool IsActive { get; init; } = true;
}
public sealed class PatientCreateRequest : PatientUpdateRequest;
public sealed record PatientStatusRequest(bool IsActive);
public sealed record PatientResponse(Guid Id, Guid? UserId, string FirstName, string LastName,
    string? Ci, DateOnly? BirthDate, string? Gender, string? Phone, string? Email, string? Address,
    string? EmergencyContactName, string? EmergencyContactPhone, string? BloodType, string? Notes,
    bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record PatientListResponse(PatientResponse[] Items, int Total, int Page, int PageSize);
