using ClinicaRamirez.Api.Application.Inventory;
using ClinicaRamirez.Api.DTOs.Inventory;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController,Authorize(Roles="ADMINISTRATOR"),Route("api/admin/inventory"),RequirePermission("inventory.view")]
public sealed class InventoryLotsController(InventoryLotService service):ControllerBase
{
    [HttpGet("lots"),RequirePermission("inventory.movement.view")]
    public Task<LotDto[]> List(Guid? itemId,Guid? centerId,Guid? categoryId,string? status,string? search,DateOnly? from,DateOnly? to,int days=30)=>service.List(itemId,centerId,categoryId,status,search,from,to,days);
    [HttpGet("expiration-alerts"),RequirePermission("inventory.movement.view")]
    public Task<ExpirationAlerts> Alerts(Guid? centerId)=>service.Alerts(centerId);
    [HttpPost("lots"),RequirePermission("inventory.item.manage")]
    public Task<Guid> Create(LotInput input)=>service.Create(input);
    [HttpPut("lots/{id:guid}"),RequirePermission("inventory.item.manage")]
    public async Task<IActionResult> Update(Guid id,LotUpdate input){await service.Update(id,input);return NoContent();}
    [HttpPost("items/{id:guid}/initialize-lots"),RequirePermission("inventory.item.manage")]
    public async Task<IActionResult> Initialize(Guid id,InitializeLotsInput input){await service.Initialize(id,input);return NoContent();}
}
