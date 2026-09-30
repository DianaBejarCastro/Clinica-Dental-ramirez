using System.Security.Claims;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.Identity;

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed class IdentitySupport(AppDbContext db, IHttpContextAccessor accessor)
{
    public Guid? Actor => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue("sub"), out var id) ? id : null;
    public string? Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public void Audit(string action, string entity, Guid? entityId, Guid? actor = null, object? metadata = null)
    {
        var agent = accessor.HttpContext?.Request.Headers.UserAgent.ToString();
        db.AuditLogs.Add(new AuditLog {
            UserId = actor ?? Actor, Action = action, Entity = entity, EntityId = entityId,
            IpAddress = Ip, UserAgent = agent?[..Math.Min(agent.Length, 512)],
            MetadataJson = metadata is null ? null : System.Text.Json.JsonSerializer.Serialize(metadata)
        });
    }
    public async Task<CurrentUserResponse> Current(Guid id)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(x => x.Id == id);
        var roles = await db.UserRoles.Where(x => x.UserId == id && x.Role.IsActive).Select(x => x.Role.Code).ToArrayAsync();
        var permissions = await db.UserRoles.Where(x => x.UserId == id && x.Role.IsActive)
            .SelectMany(x => x.Role.RolePermissions.Select(p => p.Permission.Code)).Distinct().ToArrayAsync();
        return new(user.Id, user.FirstName, user.LastName, user.Email, roles, permissions);
    }
    public static UserResponse UserDto(User u) => new(u.Id, u.FirstName, u.LastName, u.Email, u.Phone,
        u.IsActive, u.EmailVerified, u.CreatedAt, u.UpdatedAt, u.LastLoginAt,
        u.UserRoles.Select(r => new RoleSummary(r.RoleId, r.Role.Code, r.Role.Name)).ToArray());
    public static RoleResponse RoleDto(Role r) => new(r.Id, r.Code, r.Name, r.Description,
        r.IsSystem, r.IsActive, r.RolePermissions.Select(x => x.PermissionId).ToArray());
    public async Task RevokeSessions(Guid userId)
    {
        var now = DateTime.UtcNow;
        await db.RefreshTokens.Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now).SetProperty(x => x.RevokedByIp, Ip));
    }
    // Serializa decisiones sobre el último administrador y el bootstrap, incluso con varias instancias.
    public Task LockAdministration() => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(820020)");
    public Task LockSessions(Guid userId) => db.Database.ExecuteSqlInterpolatedAsync(
        $"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 820021))");
    public async Task PreserveAdministrator(Guid userId)
    {
        var isAdmin = await db.UserRoles.AnyAsync(x => x.UserId == userId && x.User.IsActive && x.Role.Code == "ADMINISTRATOR");
        if (isAdmin && !await db.UserRoles.AnyAsync(x => x.UserId != userId && x.User.IsActive && x.Role.Code == "ADMINISTRATOR"))
            throw new ApiException(409, "Debe conservarse al menos un administrador activo.");
    }
}
