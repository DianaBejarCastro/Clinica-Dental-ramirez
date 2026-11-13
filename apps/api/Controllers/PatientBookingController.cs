using ClinicaRamirez.Api.Application.Appointments;
using ClinicaRamirez.Api.DTOs.Patients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController,Authorize,Route("api/patient/appointments")]
public sealed class PatientBookingController(AppointmentsService service):ControllerBase
{
 [HttpGet("availability")]
 public async Task<IActionResult> Availability(Guid featuredDoctorId,int year,int month,CancellationToken ct) => Ok(await service.PatientAvailability(featuredDoctorId,year,month,ct));
 [HttpPost]
 public async Task<IActionResult> Book(PatientBookingRequest request,CancellationToken ct) => StatusCode(201,await service.PatientBook(request,ct));
}
