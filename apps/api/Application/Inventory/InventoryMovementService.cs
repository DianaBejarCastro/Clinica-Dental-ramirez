using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Inventory;
using ClinicaRamirez.Api.DTOs.Inventory;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.Inventory;
public sealed class InventoryMovementService(AppDbContext db,IdentitySupport identity,InventoryService inventory,InventoryLotService lots)
{
    public async Task<Guid> Create(string type,MovementInput input,bool doctor=false,string? creationFingerprint=null) {
        if(type is not ("ENTRY" or "WITHDRAWAL" or "RETURN" or "ADJUSTMENT"))throw new ApiException(400,"Tipo de movimiento inválido.");
        await inventory.Authorize(doctor?"inventory.doctor.withdraw":"inventory."+type.ToLowerInvariant()+".create");
        var recipient=doctor?await inventory.Doctor():input.RecipientDoctorProfileId;
        if(doctor && type!="WITHDRAWAL")throw new ApiException(403,"Operación no permitida.");
        if(input.IdempotencyKey==Guid.Empty || input.Lines is not {Length:>0 and <=100} ||
            input.Lines.Any(x=>x==null) || input.Lines.Select(x=>x.InventoryItemId).Distinct().Count()!=input.Lines.Length)
            throw new ApiException(400,"Selecciona artículos distintos y cantidades válidas.");
        foreach(var line in input.Lines)InventoryService.Quantity(line.Quantity);
        if(type=="WITHDRAWAL" && recipient==null)throw new ApiException(400,"Selecciona el doctor receptor.");
        if(type=="ADJUSTMENT") {
            if(input.AdjustmentDirection is not ("INCREASE" or "DECREASE") || string.IsNullOrWhiteSpace(input.Notes))
                throw new ApiException(400,"El ajuste requiere dirección y motivo.");
        } else if(input.AdjustmentDirection!=null)throw new ApiException(400,"Dirección exclusiva para ajustes.");
        var fingerprint=creationFingerprint??Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new{type,input.CenterId,recipient,Notes=input.Notes?.Trim(),input.AdjustmentDirection,Lines=input.Lines.OrderBy(x=>x.InventoryItemId).ToArray()}))));
        var actor=inventory.Actor;
        await using var tx=db.Database.CurrentTransaction==null?await db.Database.BeginTransactionAsync():null;
        // Serializes retries of one request, without serializing unrelated stock operations.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.ToString()+input.IdempotencyKey.ToString()}, 820030))");
        var prior=await db.Set<InventoryMovement>().SingleOrDefaultAsync(x=>x.CreatedByUserId==actor && x.IdempotencyKey==input.IdempotencyKey);
        if(prior!=null) {
            if(prior.RequestFingerprint!=null){if(prior.RequestFingerprint!=fingerprint)throw new ApiException(409,"La solicitud ya fue utilizada con otros datos.");if(tx!=null)await tx.CommitAsync();return prior.Id;}
            if(input.Lines.Any(x=>x.InventoryLotId!=null||x.BatchNumber!=null||x.ExpirationDate!=null))throw new ApiException(409,"Solicitud histórica incompatible.");
            var old=await db.Set<InventoryMovementLine>().Where(x=>x.InventoryMovementId==prior.Id).ToArrayAsync();
            if(prior.CenterId!=input.CenterId || prior.MovementType!=type || prior.RecipientDoctorProfileId!=recipient ||
                prior.Notes!=input.Notes?.Trim() || prior.AdjustmentDirection!=input.AdjustmentDirection ||
                old.Length!=input.Lines.Length || old.Any(x=>!input.Lines.Any(l=>l.InventoryItemId==x.InventoryItemId && l.Quantity==x.Quantity)))
                throw new ApiException(409,"La solicitud ya fue utilizada con otros datos.");
            if(tx!=null)await tx.CommitAsync();return prior.Id;
        }
        if(!await db.Centers.AnyAsync(x=>x.Id==input.CenterId && x.IsActive))throw new ApiException(400,"Sucursal no disponible.");
        if(recipient!=null && !await db.DoctorCenters.AnyAsync(x=>x.DoctorId==recipient && x.CenterId==input.CenterId && x.IsActive && x.Doctor.IsActive && x.Doctor.User.IsActive))
            throw new ApiException(403,"El doctor no está autorizado en esta sucursal.");
        foreach(var id in input.Lines.Select(x=>x.InventoryItemId).Order())await InventoryLotService.LockItem(db,id);
        var ids=input.Lines.Select(x=>x.InventoryItemId).ToArray();
        if(await (from i in db.Set<InventoryItem>() join c in db.Set<InventoryCategory>() on i.CategoryId equals c.Id
            where ids.Contains(i.Id) && i.IsActive && c.IsActive select i.Id).CountAsync()!=ids.Length)
            throw new ApiException(400,"Uno o más artículos no están disponibles.");
        var movement=new InventoryMovement{CenterId=input.CenterId,MovementType=type,CreatedByUserId=actor,
            RecipientDoctorProfileId=recipient,RequestFingerprint=fingerprint,IdempotencyKey=input.IdempotencyKey,Notes=input.Notes?.Trim(),AdjustmentDirection=input.AdjustmentDirection};
        db.Add(movement);
        // Stable lock ordering prevents deadlocks between overlapping multi-item requests.
        foreach(var line in input.Lines.OrderBy(x=>x.InventoryItemId)) {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "InventoryStocks" ("InventoryItemId","CenterId","QuantityOnHand","UpdatedAt")
                VALUES ({line.InventoryItemId},{input.CenterId},0,now()) ON CONFLICT DO NOTHING
                """);
            var stock=await db.Set<InventoryStock>().FromSqlInterpolated($"""
                SELECT * FROM "InventoryStocks" WHERE "InventoryItemId"={line.InventoryItemId} AND "CenterId"={input.CenterId} FOR UPDATE
                """).SingleAsync();
            var delta=(type=="WITHDRAWAL" || type=="ADJUSTMENT" && input.AdjustmentDirection=="DECREASE")?-line.Quantity:line.Quantity;
            if(stock.QuantityOnHand+delta<0)throw new ApiException(409,"Stock insuficiente para completar el retiro.");
            InventoryService.Quantity(stock.QuantityOnHand+delta,true);
            stock.QuantityOnHand+=delta;stock.UpdatedAt=DateTime.UtcNow;
            var item=await db.Set<InventoryItem>().SingleAsync(x=>x.Id==line.InventoryItemId);
            await lots.Allocate(item,line,movement,delta);
        }
        identity.Audit("inventory_"+type.ToLowerInvariant()+"_created","InventoryMovement",movement.Id,
            metadata:new{movement.CenterId,movement.RecipientDoctorProfileId});
        await db.SaveChangesAsync();if(tx!=null)await tx.CommitAsync();return movement.Id;
    }
    public async Task<MovementPage> History(bool doctor,Guid? centerId,Guid? doctorId,Guid? itemId,string? type,DateTime? from,DateTime? to,int page=1) {
        await inventory.Authorize(doctor?"inventory.doctor.withdraw":"inventory.movement.view");
        if(page<1 || from>to)throw new ApiException(400,"Filtros inválidos.");
        if(doctor)doctorId=await inventory.Doctor();
        var q=db.Set<InventoryMovement>().AsNoTracking().Where(x=>
            (centerId==null||x.CenterId==centerId)&&(doctorId==null||x.RecipientDoctorProfileId==doctorId)&&
            (type==null||x.MovementType==type)&&(!doctor||x.MovementType=="WITHDRAWAL")&&
            (from==null||x.OccurredAt>=from)&&(to==null||x.OccurredAt<=to)&&
            (itemId==null||db.Set<InventoryMovementLine>().Any(l=>l.InventoryMovementId==x.Id&&l.InventoryItemId==itemId)));
        var total=await q.CountAsync();
        var rows=await (from m in q join c in db.Centers on m.CenterId equals c.Id
            join u in db.Users on m.CreatedByUserId equals u.Id
            orderby m.OccurredAt descending,m.Id descending
            select new{M=m,Center=c.Name,Creator=u.FirstName+" "+u.LastName}).Skip((page-1)*50).Take(50).ToArrayAsync();
        var result=new List<MovementDto>();
        foreach(var row in rows) {
            var d=await db.DoctorProfiles.Where(x=>x.Id==row.M.RecipientDoctorProfileId)
                .Select(x=>new{x.ProfessionalLicense,Name=x.User.FirstName+" "+x.User.LastName}).SingleOrDefaultAsync();
            var lines=await (from l in db.Set<InventoryMovementLine>() join i in db.Set<InventoryItem>() on l.InventoryItemId equals i.Id
                where l.InventoryMovementId==row.M.Id orderby i.Name
                select new MovementLineDto(i.Id,i.Code,i.Name,i.Unit,l.Quantity,l.InventoryLotId,db.Set<InventoryLot>().Where(b=>b.Id==l.InventoryLotId).Select(b=>b.BatchNumber).FirstOrDefault(),db.Set<InventoryLot>().Where(b=>b.Id==l.InventoryLotId).Select(b=>b.ExpirationDate).FirstOrDefault())).ToArrayAsync();
            var m=row.M;
            result.Add(new(m.Id,m.CenterId,row.Center,m.MovementType,m.OccurredAt,m.CreatedByUserId,row.Creator,
                m.RecipientDoctorProfileId,d?.Name,d?.ProfessionalLicense,m.Notes,m.AdjustmentDirection,lines));
        }
        return new(result.ToArray(),total,page,50);
    }
}
