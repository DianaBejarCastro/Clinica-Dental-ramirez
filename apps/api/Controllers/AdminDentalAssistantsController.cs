using ClinicaRamirez.Api.Application.DentalAssistants;
using ClinicaRamirez.Api.DTOs.DentalAssistants;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaRamirez.Api.Controllers;

// Medical-team administration shares the existing doctors.view/create/update capability.
[ApiController, Route("api/admin/dental-assistants"), Tags("Admin Dental Assistants")]
public sealed class AdminDentalAssistantsController(DentalAssistantsService service) : ControllerBase
{
    [HttpGet, RequirePermission("doctors.view")]
    public Task<DentalAssistantListResponse> List(int page = 1, int pageSize = 10) => service.List(page, pageSize);
    [HttpGet("{id:guid}"), RequirePermission("doctors.view")]
    public Task<DentalAssistantResponse> Get(Guid id) => service.Get(id);
    [HttpPost, RequirePermission("doctors.create")]
    public async Task<ActionResult<DentalAssistantResponse>> Create(DentalAssistantCreateRequest request)
    {
        var result = await service.Create(request);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpPut("{id:guid}"), RequirePermission("doctors.update")]
    public Task<DentalAssistantResponse> Update(Guid id, DentalAssistantUpdateRequest request) => service.Update(id, request);
}
