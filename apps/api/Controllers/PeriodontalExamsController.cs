using ClinicaRamirez.Api.Application.DentalCharting;
using ClinicaRamirez.Api.DTOs.DentalCharting;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController,Authorize,Route("api/doctor")]
public sealed class PeriodontalExamsController(PeriodontalService service):ControllerBase
{
    [HttpGet("patients/{patientProfileId:guid}/periodontal-exams"),RequirePermission("clinical_history.view")]
    public Task<ExamSummary[]> History(Guid patientProfileId)=>service.History(patientProfileId);
    [HttpGet("periodontal-exams/{examId:guid}"),RequirePermission("clinical_history.view")]
    public Task<PeriodontalDetail> Get(Guid examId)=>service.Get(examId);
    [HttpPost("patients/{patientProfileId:guid}/periodontal-exams"),RequirePermission("clinical_history.create")]
    public async Task<ActionResult<PeriodontalDetail>> Create(Guid patientProfileId,ExamCreateRequest input) {var id=await service.Create(patientProfileId,input);return Created($"/api/doctor/periodontal-exams/{id}",await service.Get(id));}
    [HttpPost("periodontal-exams/{examId:guid}/new-version"),RequirePermission("clinical_history.create")]
    public async Task<ActionResult<PeriodontalDetail>> Version(Guid examId,ExamVersionRequest input) {var id=await service.Version(examId,input);return Ok(await service.Get(id));}
    [HttpPut("periodontal-exams/{examId:guid}"),RequirePermission("clinical_history.update")]
    public async Task<IActionResult> Update(Guid examId,PeriodontalUpdateRequest input) {await service.Update(examId,input);return NoContent();}
    [HttpPost("periodontal-exams/{examId:guid}/finalize"),RequirePermission("clinical_history.update")]
    public async Task<IActionResult> FinalizeExam(Guid examId,ExamRevisionRequest input) {await service.Finalize(examId,input);return NoContent();}
}
