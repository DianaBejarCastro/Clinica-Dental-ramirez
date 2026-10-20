using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.DTOs.Identity;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ClinicaRamirez.Api.Controllers;

[ApiController, Tags("Admin Staff")]
public sealed class AdminStaffController(StaffAdministrationService service, AdminService admin) : ControllerBase
{
    [HttpGet("api/admin/administrators"), RequirePermission("users.view")]
    public async Task<ActionResult<UserListResponse>> Administrators(int page=1,int pageSize=10,string? q=null) => Ok(await admin.Users(page,pageSize,q,"ADMINISTRATOR"));
    [HttpGet("api/admin/cashiers"), RequirePermission("users.view")]
    public async Task<ActionResult<UserListResponse>> Cashiers(int page=1,int pageSize=10,string? q=null) => Ok(await admin.Users(page,pageSize,q,"CASHIER"));
    [HttpGet("api/admin/secretaries"), RequirePermission("users.view")]
    public async Task<ActionResult<UserListResponse>> Secretaries(int page=1,int pageSize=10,string? q=null) => Ok(await admin.Users(page,pageSize,q,"SECRETARY"));
    [HttpPost("api/admin/administrators"), RequirePermission("users.create"), RequirePermission("roles.assign_permissions")]
    public async Task<ActionResult<UserResponse>> CreateAdministrator(StaffCreateRequest request) => Created("/api/admin/administrators",await service.Create("ADMINISTRATOR",request));
    [HttpPost("api/admin/cashiers"), RequirePermission("users.create"), RequirePermission("roles.assign_permissions")]
    public async Task<ActionResult<UserResponse>> CreateCashier(StaffCreateRequest request) => Created("/api/admin/cashiers",await service.Create("CASHIER",request));
    [HttpPost("api/admin/secretaries"), RequirePermission("users.create"), RequirePermission("roles.assign_permissions")]
    public async Task<ActionResult<UserResponse>> CreateSecretary(StaffCreateRequest request) => Created("/api/admin/secretaries",await service.Create("SECRETARY",request));

    [HttpPost("api/admin/users/{userId:guid}/doctor-profile"), RequirePermission("users.update"), RequirePermission("roles.assign_permissions"), RequirePermission("doctors.create")]
    public async Task<ActionResult<UserResponse>> Doctor(Guid userId,AddDoctorProfileRequest request) => Ok(await service.AddProfile(userId,"DOCTOR",request));
    [HttpPost("api/admin/users/{userId:guid}/dental-assistant-profile"), RequirePermission("users.update"), RequirePermission("roles.assign_permissions"), RequirePermission("doctors.create")]
    public async Task<ActionResult<UserResponse>> Assistant(Guid userId,AddDentalAssistantProfileRequest request) => Ok(await service.AddProfile(userId,"DENTAL_ASSISTANT",request));
    [HttpPost("api/admin/users/{userId:guid}/patient-profile"), RequirePermission("users.update"), RequirePermission("roles.assign_permissions"), RequirePermission("patients.create")]
    public async Task<ActionResult<UserResponse>> Patient(Guid userId,AddPatientProfileRequest request) => Ok(await service.AddProfile(userId,"PATIENT",request));
}
