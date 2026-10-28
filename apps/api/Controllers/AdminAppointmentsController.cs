using ClinicaRamirez.Api.Application.Appointments;
using ClinicaRamirez.Api.DTOs.Appointments;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ClinicaRamirez.Api.Controllers;

[ApiController, Route("api/admin/appointments"), Tags("Admin Appointments")]
public sealed class AdminAppointmentsController(AppointmentsService service) : ControllerBase
{
    [HttpGet, RequirePermission("appointments.view")]
    public async Task<ActionResult<AppointmentListResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        => Ok(await service.List(page, pageSize));
    [HttpGet("agenda"), RequirePermission("appointments.view")]
    public async Task<ActionResult<AgendaResponse>> Agenda([FromQuery, BindRequired] DateOnly date,
        [FromQuery] Guid? doctorId, [FromQuery] Guid? centerId, [FromQuery] Guid? specialtyId, [FromQuery] string? status)
        => Ok(await service.Agenda(date, doctorId, centerId, specialtyId, status));
    [HttpGet("{id:guid}"), RequirePermission("appointments.view")]
    public async Task<ActionResult<AppointmentResponse>> Get(Guid id) => Ok(await service.Get(id));
    [HttpGet("availability"), RequirePermission("appointments.view")]
    public async Task<ActionResult<AvailabilityResponse>> Availability([FromQuery, BindRequired] Guid doctorId,
        [FromQuery, BindRequired] Guid centerId, [FromQuery, BindRequired] Guid specialtyId,
        [FromQuery, BindRequired] DateOnly date, [FromQuery, BindRequired] int durationMinutes)
        => Ok(await service.Availability(doctorId, centerId, specialtyId, date, durationMinutes));
    [HttpPost, RequirePermission("appointments.create")]
    public async Task<ActionResult<AppointmentResponse>> Create(AppointmentCreateRequest request)
    {
        var appointment = await service.Create(request);
        return CreatedAtAction(nameof(Get), new { id = appointment.Id }, appointment);
    }
    [HttpPut("{id:guid}"), RequirePermission("appointments.update")]
    public async Task<ActionResult<AppointmentResponse>> Update(Guid id, AppointmentUpdateRequest request) => Ok(await service.Update(id, request));
    // El permiso depende del estado solicitado y se comprueba dentro de la transacción.
    [HttpPatch("{id:guid}/status"), Authorize]
    public async Task<IActionResult> Status(Guid id, AppointmentStatusRequest request)
    {
        await service.Status(id, request.Status);
        return NoContent();
    }
}
