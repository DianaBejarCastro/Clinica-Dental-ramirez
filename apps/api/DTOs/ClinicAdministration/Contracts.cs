using System.ComponentModel.DataAnnotations;
using ClinicaRamirez.Api.DTOs.Identity;

namespace ClinicaRamirez.Api.DTOs.ClinicAdministration;

public sealed class ClinicUpdateRequest
{
    [Required, MaxLength(200)] public string Name { get; init; } = "";
    [MaxLength(250)] public string? LegalName { get; init; }
    [MaxLength(2000)] public string? Description { get; init; }
    [MaxLength(2048), HttpUrl] public string? LogoUrl { get; init; }
    [MaxLength(30)] public string? Phone { get; init; }
    [MaxLength(30)] public string? Whatsapp { get; init; }
    [MaxLength(254), EmailAddress] public string? Email { get; init; }
    [MaxLength(50)] public string? TaxId { get; init; }
    public bool IsActive { get; init; } = true;
}
public class CenterUpdateRequest
{
    [Required, MaxLength(200)] public string Name { get; init; } = "";
    [Required, MaxLength(80), RegularExpression("^[A-Za-z][A-Za-z0-9_]*$")] public string Code { get; init; } = "";
    [MaxLength(2000)] public string? Description { get; init; }
    [Required, MaxLength(500)] public string Address { get; init; } = "";
    [MaxLength(100)] public string? City { get; init; }
    [MaxLength(100)] public string? Department { get; init; }
    [Required, MaxLength(100)] public string Country { get; init; } = "Bolivia";
    [MaxLength(30)] public string? Phone { get; init; }
    [MaxLength(30)] public string? Whatsapp { get; init; }
    [MaxLength(254), EmailAddress] public string? Email { get; init; }
    [Range(-90, 90)] public decimal? Latitude { get; init; }
    [Range(-180, 180)] public decimal? Longitude { get; init; }
    public bool IsMain { get; init; }
    public bool IsActive { get; init; } = true;
}
public sealed class CenterCreateRequest : CenterUpdateRequest;
public class SpecialtyUpdateRequest
{
    [Required, MaxLength(80), RegularExpression("^[A-Za-z][A-Za-z0-9_]*$")] public string Code { get; init; } = "";
    [Required, MaxLength(200)] public string Name { get; init; } = "";
    [MaxLength(2000)] public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}
public sealed class SpecialtyCreateRequest : SpecialtyUpdateRequest;
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public class DoctorUpdateRequest
{
    [Required, MaxLength(100)] public string FirstName { get; init; } = "";
    [Required, MaxLength(100)] public string LastName { get; init; } = "";
    [Required, MaxLength(100)] public string ProfessionalLicense { get; init; } = "";
    [Required, MaxLength(50)] public string Ci { get; init; } = "";
    [Required, ClinicaRamirez.Api.DTOs.LocalBirthDate] public DateOnly? BirthDate { get; init; }
    [MaxLength(50)] public string? Gender { get; init; }
    [Required, MaxLength(30)] public string Phone { get; init; } = "";
    [MaxLength(500)] public string? Address { get; init; }
    [MaxLength(2048), HttpUrl] public string? PhotoUrl { get; init; }
    [MaxLength(4000)] public string? Bio { get; init; }
    public bool IsActive { get; init; } = true;
}
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed class DoctorCreateRequest : DoctorUpdateRequest
{
    [Required, EmailAddress, MaxLength(254)] public string Email { get; init; } = "";
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; init; } = "";
    [Required, MaxLength(128), Compare(nameof(Password))] public string ConfirmPassword { get; init; } = "";
}
public sealed record AssignDoctorCentersRequest([Required, MaxLength(1000)] Guid[] CenterIds);
public sealed record AssignDoctorSpecialtiesRequest([Required, MaxLength(1000)] Guid[] SpecialtyIds);
public class DoctorScheduleUpdateRequest
{
    public Guid CenterId { get; init; }
    [Range(1, 7)] public int DayOfWeek { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public bool HasBreak { get; init; }
    public TimeOnly? BreakStart { get; init; }
    public TimeOnly? BreakEnd { get; init; }
    public bool IsActive { get; init; } = true;
}
public sealed class DoctorScheduleCreateRequest : DoctorScheduleUpdateRequest;
public sealed record StatusRequest(bool IsActive);

public sealed record ClinicResponse(Guid Id, string Name, string? LegalName, string? Description, string? LogoUrl,
    string? Phone, string? Whatsapp, string? Email, string? TaxId, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt)
{
    public ClinicLogoResponse? Logo { get; init; }
}
public sealed record ClinicLogoResponse(string ContentType, string FileName, long Size, DateTime UpdatedAt);
public sealed record CenterResponse(Guid Id, Guid ClinicId, string Name, string Code, string? Description, string Address,
    string? City, string? Department, string Country, string? Phone, string? Whatsapp, string? Email,
    decimal? Latitude, decimal? Longitude, bool IsMain, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record SpecialtyResponse(Guid Id, string Code, string Name, string? Description, bool IsActive,
    int DoctorCount, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record AssignmentResponse(Guid Id, string Code, string Name, bool IsActive);
public sealed record DoctorUserResponse(Guid Id, string FirstName, string LastName, string? Email, bool IsActive);
public sealed record DoctorResponse(Guid Id, Guid UserId, DoctorUserResponse User, string? ProfessionalLicense, string? Ci,
    DateOnly? BirthDate, string? Gender, string? Phone, string? Address, string? PhotoUrl, string? Bio, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt, AssignmentResponse[] Specialties, AssignmentResponse[] Centers);
public sealed record DoctorListResponse(DoctorResponse[] Items, int Total, int Page, int PageSize);
public sealed record DoctorScheduleResponse(Guid Id, Guid DoctorId, Guid CenterId, string CenterName, int DayOfWeek,
    TimeOnly StartTime, TimeOnly EndTime, bool HasBreak, TimeOnly? BreakStart, TimeOnly? BreakEnd,
    bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record DashboardResponse(int? TotalUsers, int? TotalDoctors, int? ActiveDoctors,
    int? TotalCenters, int? ActiveCenters, int? TotalSpecialties);

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class HttpUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is string text
        && (string.IsNullOrWhiteSpace(text) || Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https");
    public override string FormatErrorMessage(string name) => $"{name} debe ser una URL HTTP o HTTPS.";
}
