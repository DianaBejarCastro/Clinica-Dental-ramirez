using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Inventory;
using ClinicaRamirez.Api.DTOs.Inventory;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.Inventory;

public sealed class InventoryItemCreation(AppDbContext db,InventoryService inventory,InventoryMovementService movements)
{
    public async Task<Guid> Create(CreateItemWithStockInput input)
    {
        await inventory.Authorize("inventory.item.manage");
        await inventory.Authorize("inventory.entry.create");
        if(input.IdempotencyKey==Guid.Empty)throw new ApiException(400,"Identificador de solicitud obligatorio.");
        var fingerprint=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new{Operation="create-item-with-initial-entry",input.Item,input.InitialStock}))));
        await using var tx=await db.Database.BeginTransactionAsync();
        var actor=inventory.Actor;
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.ToString()+input.IdempotencyKey.ToString()}, 820030))");
        var prior=await db.Set<InventoryMovement>().SingleOrDefaultAsync(x=>x.CreatedByUserId==actor&&x.IdempotencyKey==input.IdempotencyKey);
        if(prior!=null)
        {
            if(prior.RequestFingerprint!=fingerprint)throw new ApiException(409,"La solicitud ya fue utilizada con otros datos.");
            var existing=await db.Set<InventoryMovementLine>().Where(x=>x.InventoryMovementId==prior.Id).Select(x=>x.InventoryItemId).SingleAsync();
            await tx.CommitAsync();return existing;
        }
        var id=await inventory.SaveItem(null,input.Item);
        var stock=input.InitialStock;
        await movements.Create("ENTRY",new(stock.CenterId,null,input.IdempotencyKey,
            [new(id,stock.Quantity,null,stock.BatchNumber,stock.ExpirationDate,stock.ManufacturedAt)],"Entrada inicial al crear artículo"),creationFingerprint:fingerprint);
        await tx.CommitAsync();return id;
    }
}
