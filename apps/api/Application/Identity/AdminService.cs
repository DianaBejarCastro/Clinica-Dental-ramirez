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
    public async Task<UserListResponse> Users(int page, int pageSize, string? search = null, string? roleCode = null)
    {
        if (page < 1 || page > 100000 || pageSize is < 1 or > 100) throw new ApiException(400, "Paginación inválida.");
        if (search?.Length > 200) throw new ApiException(400, "La búsqueda es demasiado larga.");
        var query = UserQuery.AsNoTracking();
        if (roleCode is not null) query = query.Where(x => x.UserRoles.Any(r => r.Role.Code == roleCode));
        if (!string.IsNullOrWhiteSpace(search)) {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x => (x.FirstName + " " + x.LastName).ToLower().Contains(term) || (x.Email != null && x.Email.Contains(term)) || (x.Ci != null && x.Ci.ToLower().Contains(term)));
        }
        var items = await query.OrderBy(x => x.LastName).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return new(await support.UsersDto(items), await query.CountAsync(), page, pageSize);
    }
    public async Task<UserResponse> User(Guid id) => (await support.UsersDto([
        await UserQuery.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Usuario inexistente.")])).Single();
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
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        var user = await UserQuery.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Usuario inexistente.");
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        if (user.CanLogin && email is null) throw new ApiException(400, "El email es obligatorio para una cuenta de acceso.");
        var ci = request.Ci?.Trim().ToUpperInvariant() ?? user.Ci;
        if (ci is not null && await db.Users.AnyAsync(x => x.Id != id && x.Ci == ci)) throw new ApiException(409, "El CI ya se encuentra registrado.");
        user.Ci = ci; user.BirthDate = request.BirthDate ?? user.BirthDate;
        user.Gender = request.Gender; user.Address = request.Address;
        if (await db.Users.AnyAsync(x => x.Id != id && email != null && x.Email == email)) throw new ApiException(409, "El email ya existe.");
        if (user.Email != email) user.EmailVerified = false;
        user.FirstName = request.FirstName.Trim(); user.LastName = request.LastName.Trim();
        user.Email = email; user.Phone = request.Phone?.Trim();
        if (user.UserRoles.Any(x => x.Role.Code is "PATIENT" or "DOCTOR" or "DENTAL_ASSISTANT") &&
            (string.IsNullOrWhiteSpace(user.Ci) || user.BirthDate is null || string.IsNullOrWhiteSpace(user.Phone)))
            throw new ApiException(400, "Los perfiles de dominio requieren CI, fecha de nacimiento y teléfono.");
        support.Audit("user_updated", "User", id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return await User(id);
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
        if (roles.Any(x => x.Code == "ADMINISTRATOR") && (!user.CanLogin || user.Email is null || user.PasswordHash is null))
            throw new ApiException(409, "El administrador debe tener una cuenta de acceso habilitada.");
        if (roles.Any(x => x.Code is "PATIENT" or "DOCTOR" or "DENTAL_ASSISTANT"))
            throw new ApiException(400, "PATIENT, DOCTOR y DENTAL_ASSISTANT se asignan automáticamente desde sus respectivos módulos.");
        // Domain roles are managed exclusively by their modules; preserve existing grants.
        var domainRoleIds = user.UserRoles.Where(x => x.Role.Code == "PATIENT" || x.Role.Code == "DOCTOR" || x.Role.Code == "DENTAL_ASSISTANT").Select(x => x.RoleId).ToArray();
        ids = ids.Concat(domainRoleIds).Distinct().ToArray();
        if (!roles.Any(x => x.Code == "ADMINISTRATOR")) await support.PreserveAdministrator(id);
        foreach (var removed in user.UserRoles.Where(x => !ids.Contains(x.RoleId)))
            support.Audit("user_role_removed", "User", id, metadata: new { roleCode = removed.Role.Code });
        db.UserRoles.RemoveRange(user.UserRoles.Where(x => !ids.Contains(x.RoleId)));
        foreach (var role in roles.Where(r => user.UserRoles.All(x => x.RoleId != r.Id))) {
            db.UserRoles.Add(new UserRole { UserId = id, RoleId = role.Id, AssignedByUserId = support.Actor });
            support.Audit("user_role_added", "User", id, metadata: new { roleCode = role.Code });
        }
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
        await using var tx = await db.Database.BeginTransactionAsync();
        await support.LockAdministration();
        var role = await RoleQuery.SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiException(404, "Rol inexistente.");
        if (role.Code == "ADMINISTRATOR" && !request.IsActive) throw new ApiException(409, "No se puede desactivar el rol administrador.");
        if (role.Code == "DOCTOR" && !request.IsActive && await db.DoctorProfiles.AnyAsync())
            throw new ApiException(409, "No se puede desactivar DOCTOR mientras existan perfiles profesionales.");
        if (role.Code == "PATIENT" && !request.IsActive && await db.PatientProfiles.AnyAsync())
            throw new ApiException(409, "No se puede desactivar PATIENT mientras existan pacientes vinculados.");
        if (role.Code == "DENTAL_ASSISTANT" && !request.IsActive && await db.DentalAssistantProfiles.AnyAsync())
            throw new ApiException(409, "No se puede desactivar DENTAL_ASSISTANT mientras existan perfiles.");
        role.Name = request.Name.Trim(); role.Description = request.Description?.Trim(); role.IsActive = request.IsActive;
        support.Audit("role_updated", "Role", id);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
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
