using ClinicaRamirez.Api.Application.DentalCharting;
using ClinicaRamirez.Api.DTOs.DentalCharting;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController,Authorize,Route("api/doctor")]
public sealed class OdontogramsController(OdontogramService service):ControllerBase
{
    [HttpGet("patients/{patientProfileId:guid}/odontograms"),RequirePermission("clinical_history.view")]
    public Task<ExamSummary[]> History(Guid patientProfileId)=>service.History(patientProfileId);
    [HttpGet("odontograms/{examId:guid}"),RequirePermission("clinical_history.view")]
    public Task<OdontogramDetail> Get(Guid examId)=>service.Get(examId);
    [HttpPost("patients/{patientProfileId:guid}/odontograms"),RequirePermission("clinical_history.create")]
    public async Task<ActionResult<OdontogramDetail>> Create(Guid patientProfileId,ExamCreateRequest input) {var id=await service.Create(patientProfileId,input);return Created($"/api/doctor/odontograms/{id}",await service.Get(id));}
    [HttpPost("odontograms/{examId:guid}/new-version"),RequirePermission("clinical_history.create")]
    public async Task<ActionResult<OdontogramDetail>> Version(Guid examId,ExamVersionRequest input) {var id=await service.Version(examId,input);return Ok(await service.Get(id));}
    [HttpPut("odontograms/{examId:guid}"),RequirePermission("clinical_history.update")]
    public async Task<IActionResult> Update(Guid examId,OdontogramUpdateRequest input) {await service.Update(examId,input);return NoContent();}
    [HttpPost("odontograms/{examId:guid}/finalize"),RequirePermission("clinical_history.update")]
    public async Task<IActionResult> FinalizeExam(Guid examId,ExamRevisionRequest input) {await service.Finalize(examId,input);return NoContent();}
}
