using ClinicaRamirez.Api.Application.Patients;
using ClinicaRamirez.Api.DTOs.Patients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace ClinicaRamirez.Api.Controllers;

[ApiController]
public sealed class PatientRegistrationController(PatientRegistrationService service) : ControllerBase
{
    [HttpPost("api/auth/register/patient"), AllowAnonymous, EnableRateLimiting("patient-registration")]
    public async Task<IActionResult> Register(PatientRegistrationRequest request, CancellationToken ct)
    {
        await service.Register(request, ct);
        return StatusCode(201, new { message = "Cuenta creada correctamente. Ya puedes iniciar sesión." });
    }
}
[ApiController, Authorize, Route("api/patient")]
public sealed class PatientPortalController(PatientPortalService service) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<PatientAccountResponse>> Me(CancellationToken ct) => Ok(await service.Me(ct));
    [HttpGet("appointments")]
    public async Task<ActionResult<PatientAppointmentResponse[]>> Appointments(CancellationToken ct) => Ok(await service.Appointments(ct));
}
