using System.Security.Claims;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClinicaRamirez.Api.Infrastructure.Identity;

public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute("permission:" + permission);
public sealed record PermissionRequirement(string Code) : IAuthorizationRequirement;
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string name) =>
        name.StartsWith("permission:", StringComparison.Ordinal)
        ? Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(name[11..])).Build())
        : base.GetPolicyAsync(name);
}
public sealed class PermissionHandler(AppDbContext db) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out var id)) return;
        if (await db.UserRoles.AnyAsync(x => x.UserId == id && x.User.IsActive && x.Role.IsActive
            && x.Role.RolePermissions.Any(p => p.Permission.Code == requirement.Code)))
            context.Succeed(requirement);
    }
}
