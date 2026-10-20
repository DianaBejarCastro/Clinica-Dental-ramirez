using ClinicaRamirez.Api.Application.Cashier;
using ClinicaRamirez.Api.DTOs.Cashier;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController,Authorize,Route("api/cashier"),RequirePermission("payments.view")]
public sealed class CashierController(CashierService service):ControllerBase
{
    [HttpGet("patients/search")] public Task<CashierPatient[]> Search([FromQuery]string? q,[FromQuery]int limit=10)=>service.Search(q,limit);
    [HttpGet("patients/{patientId:guid}/treatments")] public Task<CashierTreatment[]> Treatments(Guid patientId)=>service.Treatments(patientId);
    [HttpGet("patients/{patientId:guid}/payments")] public Task<CashierPayment[]> History(Guid patientId)=>service.PatientPayments(patientId);
    [HttpGet("treatments/{treatmentPlanId:guid}")] public Task<CashierDetail> Detail(Guid treatmentPlanId)=>service.Detail(treatmentPlanId);
    [HttpPost("treatments/{treatmentPlanId:guid}/payments"),RequirePermission("payments.create")]
    public async Task<IActionResult> Pay(Guid treatmentPlanId,CashierPaymentRequest input){await service.Pay(treatmentPlanId,input);return NoContent();}
}
