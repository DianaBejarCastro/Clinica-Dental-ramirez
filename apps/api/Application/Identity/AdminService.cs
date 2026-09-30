using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClinicaRamirez.Api.Application.Identity;

public sealed class AdminService(AppDbContext db, IdentitySupport support, IPasswordHasher<User> hasher)
{
    private IQueryable<User> UserQuery => db.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role);
    private IQueryable<Role> RoleQuery => db.Roles.Include(x => x.RolePermissions);
    public async Task<UserListResponse> Users(int page, int pageSize)
    {
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100) throw new ApiException(400, "Paginación inválida.");
        var items = await UserQuery.AsNoTracking().OrderBy(x => x.Email).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return new(items.Select(IdentitySupport.UserDto).ToArray(), await db.Users.CountAsync(), page, pageSize);
    }
    public async Task<UserResponse> User(Guid id) => IdentitySupport.UserDto(
        await UserQuery.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Usuario inexistente."));
    public async Task<UserResponse> Create(UserCreateRequest request)
    {
        var user = new User { FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(), Phone = request.Phone?.Trim() };
        if (await db.Users.AnyAsync(x => x.Email == user.Email)) throw new ApiException(409, "El email ya existe.");
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        support.Audit("user_created", "User", user.Id);
        await db.SaveChangesAsync();
        return IdentitySupport.UserDto(user);
    }
    public async Task<UserResponse> Update(Guid id, UserUpdateRequest request)
    {
        var user = await UserQuery.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Usuario inexistente.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.Id != id && x.Email == email)) throw new ApiException(409, "El email ya existe.");
        if (user.Email != email) user.EmailVerified = false;
        user.FirstName = request.FirstName.Trim(); user.LastName = request.LastName.Trim();
        user.Email = email; user.Phone = request.Phone?.Trim();
        support.Audit("user_updated", "User", id);
        await db.SaveChangesAsync();
        return IdentitySupport.UserDto(user);
    }
    public async Task Status(Guid id, bool active)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        await support.LockSessions(id);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Usuario inexistente.");
        if (!active) { await support.PreserveAdministrator(id); await support.RevokeSessions(id); }
        user.IsActive = active;
        support.Audit("user_status_changed", "User", id, metadata: new { isActive = active });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    public async Task UserRoles(Guid id, Guid[] roleIds)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        var user = await UserQuery.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Usuario inexistente.");
        var ids = roleIds.Distinct().ToArray();
        var roles = await db.Roles.Where(x => ids.Contains(x.Id) && x.IsActive).ToArrayAsync();
        if (roles.Length != ids.Length) throw new ApiException(400, "Roles inexistentes o inactivos.");
        if (!roles.Any(x => x.Code == "ADMINISTRATOR")) await support.PreserveAdministrator(id);
        db.UserRoles.RemoveRange(user.UserRoles.Where(x => !ids.Contains(x.RoleId)));
        foreach (var role in roles.Where(r => user.UserRoles.All(x => x.RoleId != r.Id)))
            db.UserRoles.Add(new UserRole { UserId = id, RoleId = role.Id, AssignedByUserId = support.Actor });
        support.Audit("user_roles_updated", "User", id, metadata: new { roleIds = ids });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    public async Task<RoleResponse[]> Roles() => (await RoleQuery.AsNoTracking().OrderBy(x => x.Code).ToArrayAsync()).Select(IdentitySupport.RoleDto).ToArray();
    public async Task<RoleResponse> Role(Guid id) => IdentitySupport.RoleDto(
        await RoleQuery.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Rol inexistente."));
    public async Task<RoleResponse> CreateRole(RoleCreateRequest request)
    {
        var role = new Role { Code = request.Code, Name = request.Name.Trim(), Description = request.Description?.Trim() };
        db.Roles.Add(role);
        support.Audit("role_created", "Role", role.Id);
        await db.SaveChangesAsync();
        return IdentitySupport.RoleDto(role);
    }
    public async Task<RoleResponse> UpdateRole(Guid id, RoleUpdateRequest request)
    {
        var role = await RoleQuery.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Rol inexistente.");
        if (role.Code == "ADMINISTRATOR" && !request.IsActive) throw new ApiException(409, "No se puede desactivar el rol administrador.");
        role.Name = request.Name.Trim(); role.Description = request.Description?.Trim(); role.IsActive = request.IsActive;
        support.Audit("role_updated", "Role", id);
        await db.SaveChangesAsync();
        return IdentitySupport.RoleDto(role);
    }
    public async Task RolePermissions(Guid id, Guid[] permissionIds)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        var role = await RoleQuery.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Rol inexistente.");
        var ids = permissionIds.Distinct().ToArray();
        if (await db.Permissions.CountAsync(x => ids.Contains(x.Id)) != ids.Length) throw new ApiException(400, "Permisos inexistentes.");
        if (role.Code == "ADMINISTRATOR" && ids.Length != await db.Permissions.CountAsync())
            throw new ApiException(409, "El administrador debe conservar todos los permisos.");
        db.RolePermissions.RemoveRange(role.RolePermissions.Where(x => !ids.Contains(x.PermissionId)));
        foreach (var pid in ids.Where(pid => role.RolePermissions.All(x => x.PermissionId != pid)))
            db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = pid });
        role.UpdatedAt = DateTime.UtcNow;
        support.Audit("role_permissions_updated", "Role", id, metadata: new { permissionIds = ids });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
    public Task<PermissionResponse[]> Permissions() => db.Permissions.AsNoTracking().OrderBy(x => x.Code)
        .Select(x => new PermissionResponse(x.Id, x.Code, x.Name, x.Description, x.Module)).ToArrayAsync();
}
