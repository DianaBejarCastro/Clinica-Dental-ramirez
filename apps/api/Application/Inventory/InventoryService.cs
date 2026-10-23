using ClinicaRamirez.Api.Application.Identity;
using ClinicaRamirez.Api.Domain.Inventory;
using ClinicaRamirez.Api.DTOs.Inventory;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Application.Inventory;
public sealed class InventoryService(AppDbContext db,IdentitySupport identity)
{
    public static readonly string[] Units=["UNIT","BOX","PACKAGE","BOTTLE","SYRINGE","ROLL","PAIR","KIT","BAG","TUBE"];
    public Guid Actor => identity.Actor ?? throw new ApiException(401,"Inicia sesión.");
    public async Task<Guid> Doctor() => await db.DoctorProfiles.Where(x=>x.UserId==Actor && x.IsActive && x.User.IsActive)
        .Select(x=>(Guid?)x.Id).SingleOrDefaultAsync() ?? throw new ApiException(403,"Perfil de doctor no disponible.");
    public async Task Authorize(string permission) {
        if(!await db.UserRoles.AnyAsync(x=>x.UserId==Actor && x.User.IsActive && x.Role.IsActive &&
            x.Role.RolePermissions.Any(r=>r.Permission.Code==permission))) throw new ApiException(403,"No tienes permiso para esta operación.");
    }
    public async Task<InventoryOptions> Options(bool doctor) {
        await Authorize("inventory.view");
        var id=doctor?await Doctor():(Guid?)null;
        var centers=await db.Centers.Where(x=>x.IsActive && (id==null || db.DoctorCenters.Any(d=>d.DoctorId==id && d.CenterId==x.Id && d.IsActive)))
            .OrderBy(x=>x.Name).Select(x=>new CenterOption(x.Id,x.Name)).ToArrayAsync();
        var categories=await db.Set<InventoryCategory>().OrderBy(x=>x.DisplayOrder).ThenBy(x=>x.Name)
            .Select(x=>new CategoryDto(x.Id,x.Code,x.Name,x.Description,x.IsActive,x.DisplayOrder)).ToArrayAsync();
        DoctorOption[] doctors=[];
        if(!doctor) doctors=await db.DoctorProfiles.Where(x=>x.IsActive && x.User.IsActive).OrderBy(x=>x.User.LastName)
            .Select(x=>new DoctorOption(x.Id,x.User.FirstName+" "+x.User.LastName,x.ProfessionalLicense,
                x.DoctorCenters.Where(c=>c.IsActive).Select(c=>c.CenterId).ToArray())).ToArrayAsync();
        return new(categories,centers,doctors);
    }
    public Task<ItemDto[]> Items() => (from i in db.Set<InventoryItem>().AsNoTracking()
        join c in db.Set<InventoryCategory>() on i.CategoryId equals c.Id orderby i.Name
        select new ItemDto(i.Id,i.CategoryId,i.Code,i.Name,c.Name,i.Description,i.Unit,i.MinimumStock,i.IsActive)).ToArrayAsync();
    public async Task<StockDto[]> Stocks(bool doctor,Guid? centerId,Guid? categoryId,string? status,string? search) {
        await Authorize("inventory.view");
        var id=doctor?await Doctor():(Guid?)null;
        var q=from i in db.Set<InventoryItem>().AsNoTracking()
              join category in db.Set<InventoryCategory>() on i.CategoryId equals category.Id
              from center in db.Centers.Where(c=>c.IsActive && (centerId==null || c.Id==centerId) &&
                  (id==null || db.DoctorCenters.Any(d=>d.DoctorId==id && d.CenterId==c.Id && d.IsActive)))
              join stock in db.Set<InventoryStock>() on new {InventoryItemId=i.Id,CenterId=center.Id} equals new {stock.InventoryItemId,stock.CenterId} into stocks
              from stock in stocks.DefaultIfEmpty()
              where (!doctor || i.IsActive && category.IsActive) && (categoryId==null || category.Id==categoryId)
              select new {InventoryItemId=i.Id,CenterId=center.Id,Center=center.Name,i.Code,i.Name,CategoryId=category.Id,Category=category.Name,i.Unit,
                  QuantityOnHand=stock==null?0:stock.QuantityOnHand,i.MinimumStock,
                  Status=stock==null || stock.QuantityOnHand==0?"OUT_OF_STOCK":stock.QuantityOnHand<=i.MinimumStock?"LOW_STOCK":"AVAILABLE",i.IsActive};
        if(!string.IsNullOrWhiteSpace(search)) { var term=search.Trim().ToLowerInvariant();q=q.Where(x=>x.Code.ToLower().Contains(term)||x.Name.ToLower().Contains(term)||x.Category.ToLower().Contains(term)); }
        if(!string.IsNullOrEmpty(status)) q=q.Where(x=>x.Status==status);
        return await q.OrderBy(x=>x.Name).ThenBy(x=>x.Center).Select(x=>new StockDto(x.InventoryItemId,x.CenterId,x.Center,x.Code,x.Name,x.CategoryId,x.Category,x.Unit,x.QuantityOnHand,x.MinimumStock,x.Status,x.IsActive)).ToArrayAsync();
    }
    public static string Text(string? value,int max) {
        var text=value?.Trim();if(string.IsNullOrEmpty(text)||text.Length>max)throw new ApiException(400,"Completa los datos obligatorios.");
        return text;
    }
    public static void Quantity(decimal value,bool zero=false) {
        if(value<(zero?0:0.001m)||value>99999999999.999m||decimal.Round(value,3)!=value)
            throw new ApiException(400,"Cantidad inválida: utiliza hasta tres decimales.");
    }
    public async Task<Guid> SaveCategory(Guid? id,CategoryInput input) {
        await Authorize("inventory.item.manage");
        var code=Text(input.Code,50).ToUpperInvariant();
        if(await db.Set<InventoryCategory>().AnyAsync(x=>x.Code==code && x.Id!=id))throw new ApiException(409,"El código ya está registrado.");
        var row=id==null?new InventoryCategory():await db.Set<InventoryCategory>().FindAsync(id)??throw new ApiException(404,"Categoría no disponible.");
        row.Code=code;row.Name=Text(input.Name,150);row.Description=input.Description?.Trim();row.IsActive=input.IsActive;row.DisplayOrder=input.DisplayOrder;row.UpdatedAt=DateTime.UtcNow;
        if(id==null)db.Add(row);
        identity.Audit(id==null?"inventory_category_created":"inventory_category_updated","InventoryCategory",row.Id);
        await db.SaveChangesAsync();return row.Id;
    }
    public async Task<Guid> SaveItem(Guid? id,ItemInput input) {
        await Authorize("inventory.item.manage");Quantity(input.MinimumStock,true);
        if(!Units.Contains(input.Unit))throw new ApiException(400,"Unidad inválida.");
        if(!await db.Set<InventoryCategory>().AnyAsync(x=>x.Id==input.CategoryId))throw new ApiException(400,"Categoría inválida.");
        var code=Text(input.Code,50).ToUpperInvariant();
        if(await db.Set<InventoryItem>().AnyAsync(x=>x.Code==code && x.Id!=id))throw new ApiException(409,"El código ya está registrado.");
        var row=id==null?new InventoryItem():await db.Set<InventoryItem>().FindAsync(id)??throw new ApiException(404,"Artículo no disponible.");
        if(id!=null && row.Unit!=input.Unit && await db.Set<InventoryMovementLine>().AnyAsync(x=>x.InventoryItemId==id))
            throw new ApiException(409,"No se puede cambiar la unidad de un artículo con movimientos.");
        row.CategoryId=input.CategoryId;row.Code=code;row.Name=Text(input.Name,150);row.Description=input.Description?.Trim();
        row.Unit=input.Unit;row.MinimumStock=input.MinimumStock;row.IsActive=input.IsActive;row.UpdatedAt=DateTime.UtcNow;
        if(id==null)db.Add(row);
        identity.Audit(id==null?"inventory_item_created":"inventory_item_updated","InventoryItem",row.Id);
        await db.SaveChangesAsync();return row.Id;
    }
}
