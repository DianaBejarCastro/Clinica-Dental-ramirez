using ClinicaRamirez.Api.Application.ClinicalCare;
using ClinicaRamirez.Api.DTOs.ClinicalCare;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController,Authorize,Route("api/doctor/appointments/{appointmentId:guid}/clinical")]
public sealed class ClinicalCareController(ClinicalCareService service):ControllerBase
{
    [HttpGet,RequirePermission("clinical_history.view")]
    public Task<ClinicalContext> Get(Guid appointmentId)=>service.Get(appointmentId);
    [HttpGet("history"),RequirePermission("clinical_history.view")]
    public Task<ClinicalHistory> History(Guid appointmentId)=>service.History(appointmentId);
    [HttpPost("start"),RequirePermission("clinical_history.create")]
    public async Task<IActionResult> Start(Guid appointmentId,StartEncounterRequest input){await service.Start(appointmentId,input);return NoContent();}
    [HttpPost("medical-history"),RequirePermission("clinical_history.update")]
    public async Task<IActionResult> Review(Guid appointmentId,ReviewInput input){await service.UpdateHistory(appointmentId,input);return NoContent();}
    [HttpPut("soap"),RequirePermission("clinical_history.update")]
    public async Task<IActionResult> Soap(Guid appointmentId,SoapInput input){await service.SaveSoap(appointmentId,input);return NoContent();}
    [HttpPost("soap/addenda"),RequirePermission("clinical_history.update")]
    public async Task<IActionResult> Addendum(Guid appointmentId,AddendumInput input){await service.Addendum(appointmentId,input);return NoContent();}
    [HttpPost("plan"),RequirePermission("treatments.create")]
    public async Task<IActionResult> Plan(Guid appointmentId,PlanInput input){await service.CreatePlan(appointmentId,input);return NoContent();}
    [HttpPost("plan/items"),RequirePermission("treatments.update")]
    public async Task<IActionResult> Item(Guid appointmentId,ProcedureInput input){await service.AddProcedure(appointmentId,input);return NoContent();}
    [HttpPatch("plan/status"),RequirePermission("treatments.update")]
    public async Task<IActionResult> Status(Guid appointmentId,ClinicalStatusInput input){await service.PlanStatus(appointmentId,input);return NoContent();}
    [HttpPost("payments"),RequirePermission("payments.create"),RequirePermission("payments.view")]
    public async Task<IActionResult> Payment(Guid appointmentId,PaymentInput input){await service.Payment(appointmentId,input);return NoContent();}
}
