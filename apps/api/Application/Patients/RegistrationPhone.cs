using System.Text.RegularExpressions;
using ClinicaRamirez.Api.Application.Identity;
namespace ClinicaRamirez.Api.Application.Patients;

public static class RegistrationPhone
{
    private static readonly (string Code, int Min, int Max)[] Rules = [("591", 8, 8), ("51", 9, 9), ("56", 9, 9), ("54", 10, 11), ("55", 10, 11)];
    public static void Validate(string? value)
    {
        // Older clients may still submit local Bolivian numbers. Existing records are never rewritten.
        if (value is not null && Regex.IsMatch(value, @"\A[0-9]{8}\z")) return;
        if (value is not null && Regex.IsMatch(value, @"\A\+[1-9][0-9]{1,14}\z"))
            foreach (var rule in Rules)
                if (value.StartsWith("+" + rule.Code, StringComparison.Ordinal) && value.Length - rule.Code.Length - 1 >= rule.Min && value.Length - rule.Code.Length - 1 <= rule.Max) return;
        throw new ApiException(400, "Número de teléfono inválido: revisa el código de país y la cantidad de dígitos.");
    }
}
