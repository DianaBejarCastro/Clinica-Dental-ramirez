using ClinicaRamirez.Api.Application.Patients;
using ClinicaRamirez.Api.DTOs.Patients;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaRamirez.Api.Controllers;

[ApiController, Route("api/admin/patients"), Tags("Admin Patients")]
public sealed class AdminPatientsController(PatientsService service) : ControllerBase
{
    [HttpGet("search"), RequirePermission("patients.view")]
    public async Task<ActionResult<PatientSearchResult[]>> Search([FromQuery] string? q, [FromQuery] int limit = 10, CancellationToken ct = default)
        => Ok(await service.Search(q, limit, ct));
    [HttpGet, RequirePermission("patients.view")]
    public async Task<ActionResult<PatientListResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50) => Ok(await service.List(page, pageSize));
    [HttpGet("{id:guid}"), RequirePermission("patients.view")]
    public async Task<ActionResult<PatientResponse>> Get(Guid id) => Ok(await service.Get(id));
    [HttpPost, RequirePermission("patients.create")]
    public async Task<ActionResult<PatientResponse>> Create(PatientCreateRequest request)
    {
        var patient = await service.Create(request);
        return CreatedAtAction(nameof(Get), new { id = patient.Id }, patient);
    }
    [HttpPut("{id:guid}"), RequirePermission("patients.update")]
    public async Task<ActionResult<PatientResponse>> Update(Guid id, PatientUpdateRequest request) => Ok(await service.Update(id, request));
    [HttpPatch("{id:guid}/status"), RequirePermission("patients.disable")]
    public async Task<IActionResult> Status(Guid id, PatientStatusRequest request) { await service.Status(id, request.IsActive); return NoContent(); }
    [HttpPost("{id:guid}/account"), RequirePermission("patients.update")]
    public async Task<ActionResult<PatientResponse>> CreateAccount(Guid id, PatientAccountCreateRequest request) => Ok(await service.CreateAccount(id, request));
}
