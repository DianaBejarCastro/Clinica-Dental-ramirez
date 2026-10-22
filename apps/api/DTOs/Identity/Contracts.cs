using System.ComponentModel.DataAnnotations;
namespace ClinicaRamirez.Api.DTOs.Identity;

public record LoginRequest(
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, MaxLength(128)] string Password);
[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public record UserCreateRequest(
    [Required, StringLength(100, MinimumLength = 1)] string FirstName,
    [Required, StringLength(100, MinimumLength = 1)] string LastName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [MaxLength(30)] string? Phone);
public record UserUpdateRequest(
    [Required, StringLength(100, MinimumLength = 1)] string FirstName,
    [Required, StringLength(100, MinimumLength = 1)] string LastName,
    [EmailAddress, MaxLength(254)] string? Email,
    [MaxLength(30)] string? Phone)
{
    [MaxLength(50), MinLength(1)] public string? DocumentNumber { get; init; }
    public string? DocumentType { get; init; }
    [MaxLength(2)] public string? DocumentCountryCode { get; init; }
    [LocalBirthDate] public DateOnly? BirthDate { get; init; }
    [MaxLength(50)] public string? Gender { get; init; }
    [MaxLength(500)] public string? Address { get; init; }
}
public record UserStatusRequest(bool IsActive);
public record RoleCreateRequest(
    [Required, RegularExpression("^[A-Z][A-Z0-9_]*$"), MaxLength(80)] string Code,
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [MaxLength(500)] string? Description);
public record RoleUpdateRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [MaxLength(500)] string? Description, bool IsActive);
public record AssignUserRolesRequest([Required, MaxLength(100)] Guid[] RoleIds);
public record AssignRolePermissionsRequest([Required, MaxLength(200)] Guid[] PermissionIds);
public record RoleSummary(Guid Id, string Code, string Name);
public record UserResponse(Guid Id, string FirstName, string LastName, string? Email, string? Phone,
    bool IsActive, bool EmailVerified, DateTime CreatedAt, DateTime UpdatedAt, DateTime? LastLoginAt, RoleSummary[] Roles)
{
    public string? DocumentNumber { get; init; }
    public string? DocumentType { get; init; }
    [MaxLength(2)] public string? DocumentCountryCode { get; init; }
    public DateOnly? BirthDate { get; init; }
    public string? Gender { get; init; }
    public string? Address { get; init; }
    public bool CanLogin { get; init; }
    public bool HasDoctorProfile { get; init; }
    public bool HasPatientProfile { get; init; }
    public bool HasDentalAssistantProfile { get; init; }
}
public record UserListResponse(UserResponse[] Items, int Total, int Page, int PageSize);
public record PermissionResponse(Guid Id, string Code, string Name, string? Description, string Module);
public record RoleResponse(Guid Id, string Code, string Name, string? Description, bool IsSystem,
    bool IsActive, Guid[] PermissionIds);
public record CurrentUserResponse(Guid Id, string FirstName, string LastName, string Email, string[] Roles, string[] Permissions);
public record LoginResponse(string AccessToken, DateTime ExpiresAt, CurrentUserResponse User);
