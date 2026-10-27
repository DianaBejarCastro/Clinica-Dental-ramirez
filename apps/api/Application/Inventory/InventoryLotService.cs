using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Inventory;
using ClinicaRamirez.Api.DTOs.Inventory;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.Inventory;

public sealed class InventoryLotService(AppDbContext db, IdentitySupport identity, InventoryService inventory)
{
    public static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow,"America/La_Paz"));
    public static string Status(DateOnly? date,int days=30) => date is null?"NO_EXPIRATION":date<Today?"EXPIRED":date<=Today.AddDays(days)?"EXPIRING":"VALID";
    public static Task LockItem(AppDbContext db,Guid id) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({id.ToString()}, 820031))");
    private async Task Manage() {
        await inventory.Authorize("inventory.item.manage");
        if(!await db.UserRoles.AnyAsync(x=>x.UserId==inventory.Actor&&x.Role.IsActive&&x.Role.Code=="ADMINISTRATOR"))throw new ApiException(403,"Solo administración puede gestionar lotes.");
    }
    public static void ValidateDates(bool expiration,DateOnly? expires,DateOnly? manufactured) {
        if(expiration && expires is null)throw new ApiException(400,"La fecha de vencimiento es obligatoria.");
        if(!expiration && expires is not null)throw new ApiException(400,"El artículo no controla vencimiento.");
        if(manufactured>Today || manufactured>expires)throw new ApiException(400,"Fecha de fabricación inválida.");
    }
    // Caller holds the item lock inside its transaction; one actual batch is shared across branches.
    public async Task<InventoryLot> Resolve(InventoryItem item,LotInput input) {
        if(!item.TracksLots || item.Id!=input.InventoryItemId)throw new ApiException(400,"El lote no corresponde al artículo.");
        ValidateDates(item.TracksExpiration,input.ExpirationDate,input.ManufacturedAt);
        var number=InventoryService.Text(input.BatchNumber,100).ToUpperInvariant();
        var supplier=input.SupplierName?.Trim();if(supplier?.Length>200)throw new ApiException(400,"Proveedor demasiado largo.");
        var lot=db.Set<InventoryLot>().Local.FirstOrDefault(x=>x.InventoryItemId==item.Id&&x.BatchNumber==number)
            ?? await db.Set<InventoryLot>().SingleOrDefaultAsync(x=>x.InventoryItemId==item.Id&&x.BatchNumber==number);
        if(lot!=null) {
            if(!lot.IsActive || lot.ExpirationDate!=input.ExpirationDate || lot.ManufacturedAt!=input.ManufacturedAt || lot.SupplierName!=supplier)
                throw new ApiException(409,"El lote ya existe con otros datos. Corrígelo con un motivo en Lotes.");
            return lot;
        }
        lot=new(){InventoryItemId=item.Id,BatchNumber=number,ExpirationDate=input.ExpirationDate,ManufacturedAt=input.ManufacturedAt,SupplierName=supplier};db.Add(lot);
        identity.Audit("inventory_lot_created","InventoryLot",lot.Id,metadata:new{ItemId=item.Id,LotId=lot.Id});return lot;
    }
    public async Task<Guid> Create(LotInput input) {
        await Manage();await using var tx=await db.Database.BeginTransactionAsync();await LockItem(db,input.InventoryItemId);
        var item=await db.Set<InventoryItem>().FindAsync(input.InventoryItemId)??throw new ApiException(404,"Artículo inexistente.");
        var lot=await Resolve(item,input);await db.SaveChangesAsync();await tx.CommitAsync();return lot.Id;
    }
    public async Task Update(Guid id,LotUpdate input) {
        await Manage();var itemId=await db.Set<InventoryLot>().Where(x=>x.Id==id).Select(x=>(Guid?)x.InventoryItemId).SingleOrDefaultAsync()??throw new ApiException(404,"Lote inexistente.");
        await using var tx=await db.Database.BeginTransactionAsync();await LockItem(db,itemId);
        var lot=await db.Set<InventoryLot>().SingleAsync(x=>x.Id==id);var item=await db.Set<InventoryItem>().SingleAsync(x=>x.Id==itemId);
        var reason=InventoryService.Text(input.Reason,2000);ValidateDates(item.TracksExpiration,input.ExpirationDate,input.ManufacturedAt);
        if(input.SupplierName?.Length>200)throw new ApiException(400,"Proveedor demasiado largo.");
        lot.ExpirationDate=input.ExpirationDate;lot.ManufacturedAt=input.ManufacturedAt;lot.SupplierName=input.SupplierName?.Trim();lot.IsActive=input.IsActive;lot.UpdatedAt=DateTime.UtcNow;
        identity.Audit("inventory_lot_updated","InventoryLot",id,metadata:new{ItemId=itemId,LotId=id,Reason=reason});await db.SaveChangesAsync();await tx.CommitAsync();
    }
    public async Task Initialize(Guid itemId,InitializeLotsInput input) {
        await Manage();var reason=InventoryService.Text(input.Reason,2000);
        if(input.Allocations is not {Length:>0 and <=100} || input.Allocations.Any(x=>x is null || x.Lot is null))throw new ApiException(400,"Asigna el stock a lotes reales.");
        await using var tx=await db.Database.BeginTransactionAsync();await LockItem(db,itemId);
        var item=await db.Set<InventoryItem>().SingleOrDefaultAsync(x=>x.Id==itemId)??throw new ApiException(404,"Artículo inexistente.");
        if(item.TracksLots)throw new ApiException(409,"El seguimiento ya está habilitado.");
        var stocks=await db.Set<InventoryStock>().Where(x=>x.InventoryItemId==itemId).ToArrayAsync();
        foreach(var a in input.Allocations) { InventoryService.Quantity(a.Quantity);if(a.Lot.InventoryItemId!=itemId)throw new ApiException(400,"Artículo incorrecto."); }
        var sums=input.Allocations.GroupBy(x=>x.CenterId).ToDictionary(x=>x.Key,x=>x.Sum(a=>a.Quantity));
        if(stocks.Any(s=>s.QuantityOnHand!=sums.GetValueOrDefault(s.CenterId)) || sums.Any(s=>!stocks.Any(x=>x.CenterId==s.Key&&x.QuantityOnHand==s.Value)))
            throw new ApiException(409,"La asignación debe coincidir exactamente con el stock de cada sucursal.");
        item.TracksLots=true;item.TracksExpiration=input.TracksExpiration;item.UpdatedAt=DateTime.UtcNow;
        foreach(var a in input.Allocations) {
            var lot=await Resolve(item,a.Lot);
            var stock=db.Set<InventoryLotStock>().Local.FirstOrDefault(x=>x.InventoryLotId==lot.Id&&x.CenterId==a.CenterId);
            if(stock==null){stock=new(){InventoryLotId=lot.Id,CenterId=a.CenterId};db.Add(stock);}stock.QuantityOnHand+=a.Quantity;
            identity.Audit("inventory_lot_stock_adjusted","InventoryLot",lot.Id,metadata:new{ItemId=itemId,LotId=lot.Id,a.CenterId,Reason=reason});
        }
        await db.SaveChangesAsync();await tx.CommitAsync();
    }
    public async Task<LotDto[]> List(Guid? itemId,Guid? centerId,Guid? categoryId,string? status,string? search,DateOnly? fromDate,DateOnly? to,int days=30) {
        await inventory.Authorize("inventory.movement.view");if(days is not (30 or 60 or 90)||fromDate>to)throw new ApiException(400,"Filtros inválidos.");
        var q=from l in db.Set<InventoryLot>().AsNoTracking() join i in db.Set<InventoryItem>() on l.InventoryItemId equals i.Id
            join s in db.Set<InventoryLotStock>() on l.Id equals s.InventoryLotId into stocks from s in stocks.DefaultIfEmpty()
            join c in db.Centers on s.CenterId equals c.Id into centers from c in centers.DefaultIfEmpty()
            where (itemId==null||i.Id==itemId)&&(centerId==null||s.CenterId==centerId)&&(categoryId==null||i.CategoryId==categoryId)
                &&(fromDate==null||l.ExpirationDate>=fromDate)&&(to==null||l.ExpirationDate<=to)
            select new {L=l,I=i,CenterId=s==null?(Guid?)null:s.CenterId,Center=c==null?null:c.Name,Quantity=s==null?0:s.QuantityOnHand};
        if(!string.IsNullOrWhiteSpace(search)){var term=search.Trim().ToUpperInvariant();q=q.Where(x=>x.L.BatchNumber.Contains(term));}
        var rows=await q.OrderBy(x=>x.L.ExpirationDate).ThenBy(x=>x.L.ReceivedAt).ToArrayAsync();
        return rows.Select(x=>new LotDto(x.L.Id,x.I.Id,x.I.Name,x.I.CategoryId,x.L.BatchNumber,x.CenterId,x.Center,x.Quantity,x.L.ReceivedAt,x.L.ExpirationDate,x.L.ManufacturedAt,x.L.SupplierName,x.L.IsActive,x.L.ExpirationDate?.DayNumber-Today.DayNumber,Status(x.L.ExpirationDate,days)))
            .Where(x=>string.IsNullOrEmpty(status)||x.Status==status).ToArray();
    }
    public async Task<ExpirationAlerts> Alerts(Guid? centerId) {
        var lots=(await List(null,centerId,null,null,null,null,null)).Where(x=>x.QuantityOnHand>0).ToArray();
        var stocks=await inventory.Stocks(false,centerId,null,null,null);
        return new(lots.Count(x=>x.DaysRemaining<0),lots.Count(x=>x.DaysRemaining>=0&&x.DaysRemaining<=30),lots.Count(x=>x.DaysRemaining>=0&&x.DaysRemaining<=60),lots.Count(x=>x.DaysRemaining>=0&&x.DaysRemaining<=90),stocks.Count(x=>x.IsActive&&x.Status=="LOW_STOCK"),stocks.Count(x=>x.IsActive&&x.Status=="OUT_OF_STOCK"));
    }
    public async Task Allocate(InventoryItem item,LineInput line,InventoryMovement movement,decimal delta) {
        if(!item.TracksLots) {
            if(line.InventoryLotId!=null||line.BatchNumber!=null||line.ExpirationDate!=null||line.ManufacturedAt!=null||line.SupplierName!=null)throw new ApiException(400,"Este artículo no utiliza lotes.");
            db.Add(new InventoryMovementLine{InventoryMovementId=movement.Id,InventoryItemId=item.Id,Quantity=line.Quantity});return;
        }
        if(movement.MovementType=="WITHDRAWAL") {
            if(line.InventoryLotId!=null||line.BatchNumber!=null||line.ExpirationDate!=null||line.ManufacturedAt!=null||line.SupplierName!=null)throw new ApiException(400,"El retiro asigna lotes automáticamente.");
            var available=await (from s in db.Set<InventoryLotStock>() join l in db.Set<InventoryLot>() on s.InventoryLotId equals l.Id
                where l.InventoryItemId==item.Id&&s.CenterId==movement.CenterId&&s.QuantityOnHand>0&&l.IsActive&&(!item.TracksExpiration||l.ExpirationDate>=Today)
                orderby l.ExpirationDate,l.ReceivedAt,l.Id select s).ToArrayAsync();
            var remaining=line.Quantity;
            foreach(var stock in available) { var take=Math.Min(remaining,stock.QuantityOnHand);if(take==0)break;stock.QuantityOnHand-=take;stock.UpdatedAt=DateTime.UtcNow;remaining-=take;
                db.Add(new InventoryMovementLine{InventoryMovementId=movement.Id,InventoryItemId=item.Id,InventoryLotId=stock.InventoryLotId,Quantity=take}); }
            if(remaining>0)throw new ApiException(409,"Stock utilizable insuficiente: los lotes vencidos o inactivos no se pueden retirar.");return;
        }
        InventoryLot lot;
        if(movement.MovementType=="ENTRY") {
            if(line.InventoryLotId!=null)throw new ApiException(400,"Registra el número real de lote en la entrada.");
            lot=await Resolve(item,new(item.Id,line.BatchNumber??"",line.ExpirationDate,line.ManufacturedAt,line.SupplierName));
        } else {
            if(line.InventoryLotId==null || line.BatchNumber!=null||line.ExpirationDate!=null||line.ManufacturedAt!=null||line.SupplierName!=null)throw new ApiException(400,"Selecciona el lote de devolución o ajuste.");
            lot=await db.Set<InventoryLot>().SingleOrDefaultAsync(x=>x.Id==line.InventoryLotId&&x.InventoryItemId==item.Id)??throw new ApiException(400,"Lote incorrecto.");
        }
        var balance=await db.Set<InventoryLotStock>().SingleOrDefaultAsync(x=>x.InventoryLotId==lot.Id&&x.CenterId==movement.CenterId);
        if(balance==null){balance=new(){InventoryLotId=lot.Id,CenterId=movement.CenterId};db.Add(balance);}
        InventoryService.Quantity(balance.QuantityOnHand+delta,true);balance.QuantityOnHand+=delta;balance.UpdatedAt=DateTime.UtcNow;
        db.Add(new InventoryMovementLine{InventoryMovementId=movement.Id,InventoryItemId=item.Id,InventoryLotId=lot.Id,Quantity=line.Quantity});
        if(movement.MovementType=="ADJUSTMENT")identity.Audit("inventory_lot_stock_adjusted","InventoryLot",lot.Id,metadata:new{ItemId=item.Id,LotId=lot.Id,movement.CenterId,MovementId=movement.Id,Reason=movement.Notes});
    }
}
