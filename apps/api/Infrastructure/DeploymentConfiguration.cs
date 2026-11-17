namespace ClinicaRamirez.Api.Infrastructure;

public static class DeploymentConfiguration
{
    public static string[] Origins(IConfiguration config, bool development)
    {
        var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (origins == null || origins.Length == 0)
        {
            if (development) return ["http://localhost:5173"];
            throw new InvalidOperationException("Configure explicit Cors:AllowedOrigins for production.");
        }
        foreach (var origin in origins)
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0
                || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/"
                || origin != uri.GetLeftPart(UriPartial.Authority) || uri.Host.Contains('*')
                || (development ? uri.Scheme is not ("https" or "http") : uri.Scheme != "https"))
                throw new InvalidOperationException("CORS origins must be explicit origins (HTTPS in production), without paths or wildcards.");
        return origins;
    }
    public static SameSiteMode CookieSameSite(IConfiguration config) =>
        config.GetValue<bool>("AuthCookies:CrossSite") ? SameSiteMode.None : SameSiteMode.Strict;
}
