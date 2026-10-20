using System.ComponentModel.DataAnnotations;

namespace ClinicaRamirez.Api.DTOs;

public sealed class LocalBirthDateAttribute : ValidationAttribute
{
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/La_Paz"));
    public override bool IsValid(object? value) => value is null || value is DateOnly date && date <= Today;
    public override string FormatErrorMessage(string name) => "La fecha de nacimiento no puede ser futura.";
}
