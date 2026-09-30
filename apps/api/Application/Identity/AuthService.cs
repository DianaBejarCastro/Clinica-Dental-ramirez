using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Infrastructure.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ClinicaRamirez.Api.Application.Identity;

public sealed class AuthService(AppDbContext db, IPasswordHasher<User> hasher, IdentitySupport support, JwtSettings jwt)
{
    private static readonly User Dummy = new();
    private static readonly string DummyHash = new PasswordHasher<User>().HashPassword(Dummy, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    public static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private RefreshToken NewToken(User user, out string raw)
    {
        raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return new RefreshToken { UserId = user.Id, TokenHash = HashToken(raw),
            ExpiresAt = DateTime.UtcNow.AddDays(jwt.RefreshDays), CreatedByIp = support.Ip };
    }
    private async Task<LoginResponse> Access(User user, RefreshToken session)
    {
        var current = await support.Current(user.Id);
        var expires = DateTime.UtcNow.AddMinutes(jwt.AccessMinutes);
        var claims = new List<Claim> {
            new("sub", user.Id.ToString()), new("user_id", user.Id.ToString()),
            new("email", user.Email), new("sid", session.Id.ToString()),
            new("jti", Guid.NewGuid().ToString())
        };
        claims.AddRange(current.Roles.Select(role => new Claim("roles", role)));
        var token = new JwtSecurityToken(jwt.Issuer, jwt.Audience, claims,
            notBefore: DateTime.UtcNow, expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, current);
    }
    public async Task<(LoginResponse Response, string Cookie, DateTime Expiry)> Login(LoginRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (user is not null) { await support.LockSessions(user.Id); await db.Entry(user).ReloadAsync(); }
        var result = hasher.VerifyHashedPassword(user ?? Dummy, user?.PasswordHash ?? DummyHash, request.Password);
        if (user is null || !user.IsActive || result == PasswordVerificationResult.Failed)
        {
            support.Audit("login_failed", "User", user?.Id, user?.Id);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            throw new ApiException(401, "Credenciales incorrectas.");
        }
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = hasher.HashPassword(user, request.Password);
        user.LastLoginAt = DateTime.UtcNow;
        var session = NewToken(user, out var raw);
        db.RefreshTokens.Add(session);
        support.Audit("login_success", "User", user.Id, user.Id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (await Access(user, session), raw, session.ExpiresAt);
    }
    public async Task<(LoginResponse Response, string Cookie, DateTime Expiry)> Refresh(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 256) throw new ApiException(401, "Sesión inválida.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var hash = HashToken(raw);
        var owner = await db.RefreshTokens.Where(x => x.TokenHash == hash).Select(x => (Guid?)x.UserId).SingleOrDefaultAsync();
        if (owner is null) throw new ApiException(401, "Sesión inválida.");
        await support.LockSessions(owner.Value);
        // Bloqueo de fila: una sola solicitud puede consumir cada token.
        var rows = await db.RefreshTokens.FromSqlInterpolated(
            $"SELECT * FROM \"RefreshTokens\" WHERE \"TokenHash\" = {hash} FOR UPDATE").ToListAsync();
        var old = rows.SingleOrDefault();
        if (old is null) throw new ApiException(401, "Sesión inválida.");
        var user = await db.Users.SingleAsync(x => x.Id == old.UserId);
        if (old.RevokedAt != null || old.ExpiresAt <= DateTime.UtcNow || !user.IsActive)
        {
            if (old.RevokedAt != null)
            {
                await support.RevokeSessions(user.Id);
                support.Audit("refresh_token_reuse_detected", "User", user.Id, user.Id);
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            throw new ApiException(401, "Sesión inválida.");
        }
        var replacement = NewToken(user, out var next);
        db.RefreshTokens.Add(replacement);
        await db.SaveChangesAsync();
        old.RevokedAt = DateTime.UtcNow;
        old.RevokedByIp = support.Ip;
        old.ReplacedByTokenId = replacement.Id;
        support.Audit("refresh_token_rotated", "User", user.Id, user.Id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (await Access(user, replacement), next, replacement.ExpiresAt);
    }
    public async Task Logout(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 256) return;
        var hash = HashToken(raw);
        var session = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash);
        if (session is null) return;
        // Revocar todas las sesiones evita que logout compita con una rotación en otra pestaña.
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockSessions(session.UserId);
        await support.RevokeSessions(session.UserId);
        support.Audit("logout", "User", session.UserId, session.UserId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
