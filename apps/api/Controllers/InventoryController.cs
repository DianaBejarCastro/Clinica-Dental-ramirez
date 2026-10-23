using ClinicaRamirez.Api.Application.Inventory;
using ClinicaRamirez.Api.DTOs.Inventory;
using ClinicaRamirez.Api.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ClinicaRamirez.Api.Controllers;
[ApiController,Authorize,Route("api/admin/inventory"),RequirePermission("inventory.view")]
public sealed class InventoryController(InventoryService service,InventoryMovementService movements):ControllerBase
{
    [HttpGet("options"),RequirePermission("inventory.movement.view")] public Task<InventoryOptions> Options()=>service.Options(false);
    [HttpGet("items"),RequirePermission("inventory.movement.view")] public Task<ItemDto[]> Items()=>service.Items();
    [HttpPost("categories"),RequirePermission("inventory.item.manage")] public Task<Guid> CreateCategory(CategoryInput input)=>service.SaveCategory(null,input);
    [HttpPut("categories/{id:guid}"),RequirePermission("inventory.item.manage")] public Task<Guid> UpdateCategory(Guid id,CategoryInput input)=>service.SaveCategory(id,input);
    [HttpPost("items"),RequirePermission("inventory.item.manage")] public Task<Guid> CreateItem(ItemInput input)=>service.SaveItem(null,input);
    [HttpPut("items/{id:guid}"),RequirePermission("inventory.item.manage")] public Task<Guid> UpdateItem(Guid id,ItemInput input)=>service.SaveItem(id,input);
    [HttpGet("stocks"),RequirePermission("inventory.movement.view")] public Task<StockDto[]> Stocks(Guid? centerId,Guid? categoryId,string? status,string? search)=>service.Stocks(false,centerId,categoryId,status,search);
    [HttpGet("movements"),RequirePermission("inventory.movement.view")]
    public Task<MovementPage> History(Guid? centerId,Guid? doctorId,Guid? itemId,string? type,DateTime? from,DateTime? to,int page=1)=>movements.History(false,centerId,doctorId,itemId,type,from,to,page);
    [HttpPost("movements/{type}")] public Task<Guid> Move(string type,MovementInput input)=>movements.Create(type.ToUpperInvariant(),input);
}
[ApiController,Authorize(Roles="DOCTOR"),Route("api/doctor/inventory"),RequirePermission("inventory.view")]
public sealed class DoctorInventoryController(InventoryService service,InventoryMovementService movements):ControllerBase
{
    [HttpGet("options")] public Task<InventoryOptions> Options()=>service.Options(true);
    [HttpGet] public Task<StockDto[]> Stocks(Guid? centerId,Guid? categoryId,string? status,string? search)=>service.Stocks(true,centerId,categoryId,status,search);
    [HttpGet("my-withdrawals"),RequirePermission("inventory.doctor.withdraw")]
    public Task<MovementPage> History(Guid? centerId,Guid? itemId,DateTime? from,DateTime? to,int page=1)=>movements.History(true,centerId,null,itemId,null,from,to,page);
    [HttpPost("withdraw"),RequirePermission("inventory.doctor.withdraw")]
    public Task<Guid> Withdraw(DoctorWithdrawalInput input)=>movements.Create("WITHDRAWAL",new(input.CenterId,null,input.IdempotencyKey,input.Lines,input.Notes),true);
}
