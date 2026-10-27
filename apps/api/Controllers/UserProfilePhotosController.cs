using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController, Route("api/admin/users/{userId:guid}/profile-photo")]
public sealed class UserProfilePhotosController(UserProfilePhotoService service) : ControllerBase
{
    [HttpGet, RequirePermission("users.view")]
    public async Task<IActionResult> Metadata(Guid userId, CancellationToken ct) => Ok(await service.Metadata(userId, ct));
    [HttpGet("content"), RequirePermission("users.view")]
    public async Task<IActionResult> Content(Guid userId, CancellationToken ct)
    {
        var photo = await service.Content(userId, ct);
        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.ETag = $"\"{photo.Version.Ticks}\"";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo.Bytes, photo.Type);
    }
    [HttpPut, RequirePermission("users.update"), Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024, MemoryBufferThreshold = 11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid userId, IFormFile file, CancellationToken ct) => Ok(await service.Upload(userId, file, ct));
    [HttpDelete, RequirePermission("users.update")]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken ct) { await service.Delete(userId, ct); return NoContent(); }
}
