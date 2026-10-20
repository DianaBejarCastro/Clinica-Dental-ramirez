using ClinicaRamirez.Api.Application.Deletion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ClinicaRamirez.Api.Controllers;

[ApiController, Authorize, Route("api/admin/deletion"), Tags("Protected Deletion")]
public sealed class ProtectedDeletionController(DeletionGuardService service) : ControllerBase
{
    [HttpGet("{entity}/{id:guid}/check")]
    public async Task<IActionResult> Check(string entity, Guid id, CancellationToken ct)
    { var result = await service.Check(entity, id, ct); return StatusCode(result.CanDelete ? 200 : 409, result); }
    [HttpDelete("{entity}/{id:guid}"), EnableRateLimiting("hard-delete")]
    public async Task<IActionResult> Delete(string entity, Guid id, ProtectedDeleteRequest request, CancellationToken ct)
    { var result = await service.Delete(entity, id, request.Password, ct); return result.CanDelete ? NoContent() : Conflict(result); }
}
