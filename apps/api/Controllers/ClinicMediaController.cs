using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController]
public sealed class ClinicMediaController(ClinicMediaService service):ControllerBase {
 [HttpGet("api/clinic/media"),AllowAnonymous] public async Task<IActionResult> Public(CancellationToken ct,string? type=null,int page=1,int pageSize=7)=>Ok(await service.List(false,type,page,pageSize,ct));
 [HttpGet("api/admin/clinic/media"),RequirePermission("clinic.view")] public async Task<IActionResult> Admin(CancellationToken ct,string? type=null,int page=1,int pageSize=8)=>Ok(await service.List(true,type,page,pageSize,ct));
 [HttpPost("api/admin/clinic/media"),RequirePermission("clinic.update"),Consumes("multipart/form-data"),RequestSizeLimit(53*1024*1024),RequestFormLimits(MultipartBodyLengthLimit=53*1024*1024)] public async Task<IActionResult> Create([FromForm]CreateMediaItemRequest request,CancellationToken ct)=>Ok(new{id=await service.Create(request,ct)});
 [HttpPut("api/admin/clinic/media/{id:guid}"),RequirePermission("clinic.update")] public async Task<IActionResult> Update(Guid id,UpdateMediaItemRequest request,CancellationToken ct){await service.Update(id,request,ct);return NoContent();}
 [HttpDelete("api/admin/clinic/media/{id:guid}"),RequirePermission("clinic.update")] public async Task<IActionResult> Delete(Guid id,CancellationToken ct){await service.Delete(id,ct);return NoContent();}
 [HttpGet("api/clinic/media/{id:guid}/content"),AllowAnonymous] public Task<IActionResult> Content(Guid id,CancellationToken ct,bool thumbnail=false)=>FileResult(id,false,thumbnail,ct);
 [HttpGet("api/admin/clinic/media/{id:guid}/content"),RequirePermission("clinic.view")] public Task<IActionResult> AdminContent(Guid id,CancellationToken ct,bool thumbnail=false)=>FileResult(id,true,thumbnail,ct);
 private async Task<IActionResult> FileResult(Guid id,bool admin,bool thumbnail,CancellationToken ct){var data=await service.Content(id,admin,thumbnail,ct);Response.Headers.CacheControl="no-store";Response.Headers.XContentTypeOptions="nosniff";return data.Url!=null?Redirect(data.Url):File(data.Bytes!,data.Mime);}
}
