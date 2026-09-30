namespace ClinicaRamirez.Api.Infrastructure.Identity;

public sealed record JwtSettings(string Secret, string Issuer, string Audience, int AccessMinutes, int RefreshDays)
{
    public static JwtSettings Load(IConfiguration config)
    {
        var secret = config["JWT_SECRET"] ?? "";
        if (secret.Length < 48 || secret == "CHANGE_ME")
            throw new InvalidOperationException("JWT_SECRET debe contener al menos 48 caracteres aleatorios.");
        var access = config.GetValue("JWT_ACCESS_TOKEN_MINUTES", 15);
        var refresh = config.GetValue("JWT_REFRESH_TOKEN_DAYS", 7);
        if (access is < 1 or > 60 || refresh is < 1 or > 30)
            throw new InvalidOperationException("Duración JWT inválida (access: 1-60 minutos; refresh: 1-30 días).");
        return new(secret, config["JWT_ISSUER"] ?? "clinica_ramirez",
            config["JWT_AUDIENCE"] ?? "clinica_ramirez_web", access, refresh);
    }
}
