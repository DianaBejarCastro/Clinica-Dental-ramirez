using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
namespace ClinicaRamirez.Api.Application.Identity;

public record GoogleIdentity(string Subject, string Email, string FirstName, string LastName);
public interface IGoogleIdentityVerifier
{
    Task<GoogleIdentity> Verify(string credential, string nonce, CancellationToken ct);
}

// Cached discovery/signing keys, with refresh on Google's signing-key rotation.
public sealed class GoogleIdentityVerifier(IConfiguration configuration) : IGoogleIdentityVerifier
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> discovery = new(
        "https://accounts.google.com/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever());
    public async Task<GoogleIdentity> Verify(string credential, string nonce, CancellationToken ct)
    {
        var audience = configuration["GoogleAuth:ClientId"];
        if (string.IsNullOrWhiteSpace(audience)) throw new ApiException(503, "Google sign-in is not configured.");
        try
        {
            var config = await discovery.GetConfigurationAsync(ct);
            try { return Validate(credential, nonce, audience, config.SigningKeys); }
            catch (SecurityTokenSignatureKeyNotFoundException)
            {
                discovery.RequestRefresh();
                config = await discovery.GetConfigurationAsync(ct);
                return Validate(credential, nonce, audience, config.SigningKeys);
            }
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException)
        { throw new ApiException(401, "Your Google account could not be verified."); }
    }
    public static GoogleIdentity Validate(string credential, string nonce, string audience, IEnumerable<SecurityKey> keys)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 16384 };
        var principal = handler.ValidateToken(credential, new TokenValidationParameters {
            ValidIssuers = ["https://accounts.google.com", "accounts.google.com"], ValidAudience = audience,
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, RequireExpirationTime = true,
            ValidateIssuerSigningKey = true, RequireSignedTokens = true, IssuerSigningKeys = keys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.Zero
        }, out _);
        string Claim(string name) => principal.FindFirst(name)?.Value ?? "";
        var email = Claim("email").Trim().ToLowerInvariant();
        if (Claim("nonce") != nonce || string.IsNullOrWhiteSpace(nonce) || Claim("email_verified") != "true"
            || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || email.Length > 254
            || string.IsNullOrWhiteSpace(Claim("sub")) || Claim("sub").Length > 255)
            throw new SecurityTokenValidationException("Invalid Google identity.");
        var first = Claim("given_name");
        if (string.IsNullOrWhiteSpace(first)) first = Claim("name");
        if (string.IsNullOrWhiteSpace(first) || first.Length > 100 || Claim("family_name").Length > 100)
            throw new SecurityTokenValidationException("Invalid Google name.");
        return new(Claim("sub"), email, first.Trim(), Claim("family_name").Trim());
    }
}
