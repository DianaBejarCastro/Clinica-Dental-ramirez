using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ClinicaRamirez.Api.DTOs.Patients;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatientRegistrationRequest(
    [Required, StringLength(100, MinimumLength = 1)] string FirstName,
    [Required, StringLength(100, MinimumLength = 1)] string LastName,
    [Required, EmailAddress, MaxLength(254)] string Email,
    [Required, StringLength(128, MinimumLength = 12)] string Password,
    [Required, MaxLength(128)] string ConfirmPassword,
    [MaxLength(50)] string? Ci = null, DateOnly? BirthDate = null,
    [MaxLength(50)] string? Gender = null, [MaxLength(30)] string? Phone = null);

public sealed record PatientAccountResponse(string FirstName, string LastName, string? Ci,
    DateOnly? BirthDate, string? Gender, string? Phone, string? Email);
public sealed record PatientAppointmentResponse(Guid Id, DateOnly AppointmentDate, TimeOnly StartTime,
    TimeOnly EndTime, string Status, string Doctor, string Center, string Specialty);
