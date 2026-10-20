using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ClinicaRamirez.Api.DTOs.Patients;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PatientRegistrationRequest : PatientClinicalRequest
{
    [Required, EmailAddress, MaxLength(254)] public string? Email { get; init; }
    [Required, StringLength(128, MinimumLength = 12)] public string Password { get; init; } = "";
    [Required, MaxLength(128), Compare(nameof(Password))] public string ConfirmPassword { get; init; } = "";
}

public sealed record PatientAccountResponse(string FirstName, string LastName, string? Ci,
    DateOnly? BirthDate, string? Gender, string? Phone, string? Email);
public sealed record PatientAppointmentResponse(Guid Id, DateOnly AppointmentDate, TimeOnly StartTime,
    TimeOnly EndTime, string Status, string Doctor, string Center, string Specialty);
