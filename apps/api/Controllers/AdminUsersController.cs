using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;

[ApiController, Route("api/admin/users"), Tags("Admin Users")]
public sealed class AdminUsersController(AdminService service) : ControllerBase
{
    [HttpGet, RequirePermission("users.view")]
    public async Task<ActionResult<UserListResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] string? q = null) => Ok(await service.Users(page, pageSize, q));
    [HttpGet("{id:guid}"), RequirePermission("users.view")]
    public async Task<ActionResult<UserResponse>> Get(Guid id) => Ok(await service.User(id));
    [HttpPost, RequirePermission("users.create")]
    public async Task<ActionResult<UserResponse>> Create(UserCreateRequest request)
    {
        var user = await service.Create(request);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }
    [HttpPut("{id:guid}"), RequirePermission("users.update")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, UserUpdateRequest request) => Ok(await service.Update(id, request));
    [HttpPatch("{id:guid}/status"), RequirePermission("users.disable")]
    public async Task<IActionResult> Status(Guid id, UserStatusRequest request) { await service.Status(id, request.IsActive); return NoContent(); }
    [HttpPut("{id:guid}/roles"), RequirePermission("users.update"), RequirePermission("roles.assign_permissions")]
    public async Task<IActionResult> Roles(Guid id, AssignUserRolesRequest request) { await service.UserRoles(id, request.RoleIds); return NoContent(); }
}
