using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;

[ApiController, Route("api/admin/permissions"), Tags("Admin Permissions")]
public sealed class AdminPermissionsController(AdminService service) : ControllerBase
{
    [HttpGet, RequirePermission("permissions.view")]
    public async Task<ActionResult<PermissionResponse[]>> List() => Ok(await service.Permissions());
}
