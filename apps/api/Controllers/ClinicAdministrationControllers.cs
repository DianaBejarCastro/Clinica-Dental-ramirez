using ClinicaRamirez.Api.Application.ClinicAdministration;
using ClinicaRamirez.Api.DTOs.ClinicAdministration;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaRamirez.Api.Controllers;

[ApiController, Route("api/admin/clinic"), Tags("Admin Clinic")]
public sealed class AdminClinicController(ClinicAdministrationService service) : ControllerBase
{
    [HttpGet, RequirePermission("clinic.view")]
    public async Task<ActionResult<ClinicResponse>> Get() => Ok(await service.Clinic());
    [HttpPut, RequirePermission("clinic.update")]
    public async Task<ActionResult<ClinicResponse>> Update(ClinicUpdateRequest request) => Ok(await service.UpdateClinic(request));
}

[ApiController, Route("api/admin/centers"), Tags("Admin Centers")]
public sealed class AdminCentersController(ClinicAdministrationService service) : ControllerBase
{
    [HttpGet, RequirePermission("centers.view")]
    public async Task<ActionResult<CenterResponse[]>> List() => Ok(await service.Centers());
    [HttpGet("{id:guid}"), RequirePermission("centers.view")]
    public async Task<ActionResult<CenterResponse>> Get(Guid id) => Ok(await service.Center(id));
    [HttpPost, RequirePermission("centers.create")]
    public async Task<ActionResult<CenterResponse>> Create(CenterCreateRequest request)
    {
        var item = await service.CreateCenter(request);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }
    [HttpPut("{id:guid}"), RequirePermission("centers.update")]
    public async Task<ActionResult<CenterResponse>> Update(Guid id, CenterUpdateRequest request) => Ok(await service.UpdateCenter(id, request));
    [HttpPatch("{id:guid}/status"), RequirePermission("centers.disable")]
    public async Task<IActionResult> Status(Guid id, StatusRequest request) { await service.CenterStatus(id, request.IsActive); return NoContent(); }
}

[ApiController, Route("api/admin/specialties"), Tags("Admin Specialties")]
public sealed class AdminSpecialtiesController(ClinicAdministrationService service) : ControllerBase
{
    [HttpGet, RequirePermission("specialties.view")]
    public async Task<ActionResult<SpecialtyResponse[]>> List() => Ok(await service.Specialties());
    [HttpGet("{id:guid}"), RequirePermission("specialties.view")]
    public async Task<ActionResult<SpecialtyResponse>> Get(Guid id) => Ok(await service.Specialty(id));
    [HttpPost, RequirePermission("specialties.create")]
    public async Task<ActionResult<SpecialtyResponse>> Create(SpecialtyCreateRequest request)
    {
        var item = await service.CreateSpecialty(request);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }
    [HttpPut("{id:guid}"), RequirePermission("specialties.update")]
    public async Task<ActionResult<SpecialtyResponse>> Update(Guid id, SpecialtyUpdateRequest request) => Ok(await service.UpdateSpecialty(id, request));
    [HttpPatch("{id:guid}/status"), RequirePermission("specialties.disable")]
    public async Task<IActionResult> Status(Guid id, StatusRequest request) { await service.SpecialtyStatus(id, request.IsActive); return NoContent(); }
}

[ApiController, Route("api/admin/doctors"), Tags("Admin Doctors")]
public sealed class AdminDoctorsController(ClinicAdministrationService service) : ControllerBase
{
    [HttpGet, RequirePermission("doctors.view")]
    public async Task<ActionResult<DoctorListResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 50) => Ok(await service.Doctors(page, pageSize));
    [HttpGet("{id:guid}"), RequirePermission("doctors.view")]
    public async Task<ActionResult<DoctorResponse>> Get(Guid id) => Ok(await service.Doctor(id));
    [HttpPost, RequirePermission("doctors.create")]
    public async Task<ActionResult<DoctorResponse>> Create(DoctorCreateRequest request)
    {
        var item = await service.CreateDoctor(request);
        return CreatedAtAction(nameof(Get), new { id = item.Id }, item);
    }
    [HttpPut("{id:guid}"), RequirePermission("doctors.update")]
    public async Task<ActionResult<DoctorResponse>> Update(Guid id, DoctorUpdateRequest request) => Ok(await service.UpdateDoctor(id, request));
    [HttpPatch("{id:guid}/status"), RequirePermission("doctors.disable")]
    public async Task<IActionResult> Status(Guid id, StatusRequest request) { await service.DoctorStatus(id, request.IsActive); return NoContent(); }
    [HttpGet("{doctorId:guid}/specialties"), RequirePermission("doctors.view")]
    public async Task<ActionResult<AssignmentResponse[]>> Specialties(Guid doctorId) => Ok(await service.DoctorSpecialties(doctorId));
    [HttpPut("{doctorId:guid}/specialties"), RequirePermission("doctors.update")]
    public async Task<IActionResult> Specialties(Guid doctorId, AssignDoctorSpecialtiesRequest request) { await service.AssignSpecialties(doctorId, request.SpecialtyIds); return NoContent(); }
    [HttpGet("{doctorId:guid}/centers"), RequirePermission("doctors.view")]
    public async Task<ActionResult<AssignmentResponse[]>> Centers(Guid doctorId) => Ok(await service.DoctorCenters(doctorId));
    [HttpPut("{doctorId:guid}/centers"), RequirePermission("doctors.update")]
    public async Task<IActionResult> Centers(Guid doctorId, AssignDoctorCentersRequest request) { await service.AssignCenters(doctorId, request.CenterIds); return NoContent(); }
}

[ApiController, Route("api/admin/doctors/{doctorId:guid}/schedules"), Tags("Admin Schedules")]
public sealed class AdminSchedulesController(ClinicAdministrationService service) : ControllerBase
{
    [HttpGet, RequirePermission("schedules.view")]
    public async Task<ActionResult<DoctorScheduleResponse[]>> List(Guid doctorId) => Ok(await service.Schedules(doctorId));
    [HttpPost, RequirePermission("schedules.create")]
    public async Task<ActionResult<DoctorScheduleResponse>> Create(Guid doctorId, DoctorScheduleCreateRequest request)
    {
        var item = await service.CreateSchedule(doctorId, request);
        return CreatedAtAction(nameof(List), new { doctorId }, item);
    }
    [HttpPut("{scheduleId:guid}"), RequirePermission("schedules.update")]
    public async Task<ActionResult<DoctorScheduleResponse>> Update(Guid doctorId, Guid scheduleId, DoctorScheduleUpdateRequest request)
        => Ok(await service.UpdateSchedule(doctorId, scheduleId, request));
    [HttpPatch("{scheduleId:guid}/status"), RequirePermission("schedules.disable")]
    public async Task<IActionResult> Status(Guid doctorId, Guid scheduleId, StatusRequest request)
    { await service.ScheduleStatus(doctorId, scheduleId, request.IsActive); return NoContent(); }
}

[ApiController, Route("api/admin/dashboard"), Tags("Admin Dashboard"), Authorize]
public sealed class AdminDashboardController(ClinicAdministrationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get() => Ok(await service.Dashboard());
}
