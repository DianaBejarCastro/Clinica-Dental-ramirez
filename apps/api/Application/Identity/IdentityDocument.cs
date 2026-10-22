using System.Globalization;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.Identity;

public static class IdentityDocument
{
    public const string ConflictMessage = "Ya existe una persona registrada con este documento de identidad.";

    public static (string? Type, string? Number, string? Country) Normalize(
        string? type, string? number, string? country, bool required = false)
    {
        if (type is null && number is null && country is null && !required) return (null, null, null);
        if (type is not ("CI" or "PASSPORT"))
            throw new ApiException(400, "Selecciona un tipo de documento válido.");
        if (string.IsNullOrWhiteSpace(number) || number.Trim().Length > 50)
            throw new ApiException(400, "El número de documento es obligatorio y admite hasta 50 caracteres.");
        number = number.Trim().ToUpperInvariant();
        if (number.Any(char.IsControl)) throw new ApiException(400, "Número de documento inválido.");
        country = country?.Trim().ToUpperInvariant();
        if (type == "CI")
        {
            if (country is not (null or "BO")) throw new ApiException(400, "El CI corresponde a Bolivia.");
            country = "BO";
        }
        else
        {
            if (country?.Length != 2 || !country.All(c => c is >= 'A' and <= 'Z'))
                throw new ApiException(400, "Selecciona el país emisor del pasaporte.");
            try
            {
                if (new RegionInfo(country).TwoLetterISORegionName != country || country == "IV")
                    throw new ArgumentException();
            }
            catch (ArgumentException) { throw new ApiException(400, "País emisor inválido."); }
        }
        return (type, number, country);
    }

    public static async Task Apply(AppDbContext db, User user, string? type, string? number, string? country, bool required = true)
    {
        var value = Normalize(type, number, country, required);
        if (value.Number != null && await db.Users.AnyAsync(x => x.Id != user.Id &&
            x.DocumentType == value.Type && x.DocumentCountryCode == value.Country && x.DocumentNumber == value.Number))
            throw new ApiException(409, ConflictMessage);
        (user.DocumentType, user.DocumentNumber, user.DocumentCountryCode) = value;
    }
}
