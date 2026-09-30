using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;

[ApiController, Route("api/admin/roles"), Tags("Admin Roles")]
public sealed class AdminRolesController(AdminService service) : ControllerBase
{
    [HttpGet, RequirePermission("roles.view")]
    public async Task<ActionResult<RoleResponse[]>> List() => Ok(await service.Roles());
    [HttpGet("{id:guid}"), RequirePermission("roles.view")]
    public async Task<ActionResult<RoleResponse>> Get(Guid id) => Ok(await service.Role(id));
    [HttpPost, RequirePermission("roles.create")]
    public async Task<ActionResult<RoleResponse>> Create(RoleCreateRequest request)
    {
        var role = await service.CreateRole(request);
        return CreatedAtAction(nameof(Get), new { id = role.Id }, role);
    }
    [HttpPut("{id:guid}"), RequirePermission("roles.update")]
    public async Task<ActionResult<RoleResponse>> Update(Guid id, RoleUpdateRequest request) => Ok(await service.UpdateRole(id, request));
    [HttpPut("{id:guid}/permissions"), RequirePermission("roles.assign_permissions")]
    public async Task<IActionResult> Permissions(Guid id, AssignRolePermissionsRequest request) { await service.RolePermissions(id, request.PermissionIds); return NoContent(); }
}
