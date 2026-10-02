using ClinicaRamirez.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaRamirez.Api.Controllers;

[ApiController]
[Route("/")]
public sealed class InfoController : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiInfoResponse> Get() => new ApiInfoResponse("Clínica Dental RAMÍREZ", "0.3.0");
}
