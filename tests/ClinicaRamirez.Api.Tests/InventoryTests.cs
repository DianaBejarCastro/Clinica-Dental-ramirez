using System.Net;
using System.Net.Http.Json;
using ClinicaRamirez.Api.Domain.Inventory;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.DTOs.Inventory;
using ClinicaRamirez.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace ClinicaRamirez.Api.Tests;
public sealed class InventoryTests(IdentityFixture f):IClassFixture<IdentityFixture>
{
    const string Base="/api/admin/inventory";
    async Task<HttpClient> Login(string? email=null) {var c=f.Client();var s=await f.Login(c,email);c.DefaultRequestHeaders.Authorization=new("Bearer",s.Body.AccessToken);return c;}
    async Task<(HttpClient Admin,HttpClient Doctor,Guid DoctorId,Guid Center,Guid Item)> Setup() {
        var admin=await Login();var email=await f.CreateUser();
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u=await db.Users.SingleAsync(x=>x.Email==email);var role=await db.Roles.SingleAsync(x=>x.Code=="DOCTOR");
        var d=new DoctorProfile{UserId=u.Id,ProfessionalLicense=Guid.NewGuid().ToString("N").ToUpperInvariant()};
        var c=new Center{ClinicId=Clinic.SingletonId,Code="INV_"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Name="Inventario prueba",Address="Prueba"};
        db.AddRange(d,c,new UserRole{UserId=u.Id,RoleId=role.Id},new DoctorCenter{DoctorId=d.Id,CenterId=c.Id,IsActive=true});
        await db.SaveChangesAsync();
        var category=await db.Set<InventoryCategory>().Select(x=>x.Id).FirstAsync();
        var result=await admin.PostAsJsonAsync(Base+"/items",new ItemInput(category,Guid.NewGuid().ToString("N"),"Guantes","Test","BOX",5));
        Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        return(admin,await Login(email),d.Id,c.Id,await result.Content.ReadFromJsonAsync<Guid>());
    }
    static MovementInput Input(Guid center,Guid item,decimal qty,Guid? doctor=null,string? notes=null,string? direction=null)=>new(center,doctor,Guid.NewGuid(),[new(item,qty)],notes,direction);
    async Task<decimal> Stock(HttpClient c,Guid center,Guid item)=> (await c.GetFromJsonAsync<StockDto[]>(Base+"/stocks?centerId="+center))!.Single(x=>x.InventoryItemId==item).QuantityOnHand;
    async Task<Guid> Move(HttpClient c,string type,MovementInput input) {var r=await c.PostAsJsonAsync(Base+"/movements/"+type,input);Assert.Equal(HttpStatusCode.OK,r.StatusCode);return await r.Content.ReadFromJsonAsync<Guid>();}
    [Fact] public async Task CategoryItemUniqueCodeAndZeroInitialStock() {
        var x=await Setup();
        var code=Guid.NewGuid().ToString("N");var cat=new CategoryInput(code,"Categoría prueba",null);
        Assert.Equal(HttpStatusCode.OK,(await x.Admin.PostAsJsonAsync(Base+"/categories",cat)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/categories",cat)).StatusCode);
        var item=(await x.Admin.GetFromJsonAsync<ItemDto[]>(Base+"/items"))!.Single(i=>i.Id==x.Item);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/items",new ItemInput(item.CategoryId,item.Code,item.Name,null,"UNIT",0))).StatusCode);
        Assert.Equal(0,await Stock(x.Admin,x.Center,x.Item));
    }
    [Fact] public async Task IsolatedSmokeEntryAdminAndDoctorWithdrawalPersistAndAudit() {
        var x=await Setup();await Move(x.Admin,"entry",Input(x.Center,x.Item,20));
        Assert.Equal(20,await Stock(x.Admin,x.Center,x.Item));
        await Move(x.Admin,"withdrawal",Input(x.Center,x.Item,3,x.DoctorId));
        Assert.Equal(17,await Stock(x.Admin,x.Center,x.Item));
        var r=await x.Doctor.PostAsJsonAsync("/api/doctor/inventory/withdraw",new DoctorWithdrawalInput(x.Center,Guid.NewGuid(),[new(x.Item,2)],null));
        Assert.Equal(HttpStatusCode.OK,r.StatusCode);Assert.Equal(15,await Stock(x.Admin,x.Center,x.Item));
        var history=(await x.Doctor.GetFromJsonAsync<MovementPage>("/api/doctor/inventory/my-withdrawals"))!;
        Assert.Equal(2,history.Total);Assert.All(history.Items,m=>{Assert.Equal(x.DoctorId,m.RecipientDoctorProfileId);Assert.NotEmpty(m.RegisteredBy);Assert.NotEmpty(m.Doctor!);Assert.True(m.OccurredAt>DateTime.UtcNow.AddMinutes(-5));});
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/movements/withdrawal",Input(x.Center,x.Item,16,x.DoctorId))).StatusCode);
        Assert.Equal(15,await Stock(x.Admin,x.Center,x.Item));
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3,await db.AuditLogs.CountAsync(a=>a.Entity=="InventoryMovement"&&db.Set<InventoryMovement>().Any(m=>m.CenterId==x.Center&&m.Id==a.EntityId)));
        Assert.All(await db.AuditLogs.Where(a=>a.Entity=="InventoryMovement").ToArrayAsync(),a=>Assert.DoesNotContain("Guantes",a.MetadataJson??""));
    }
    [Theory][InlineData("return",null,23)][InlineData("adjustment","INCREASE",23)][InlineData("adjustment","DECREASE",17)]
    public async Task ReturnsAndExplicitAdjustments(string type,string? direction,decimal expected) {
        var x=await Setup();await Move(x.Admin,"entry",Input(x.Center,x.Item,20));
        await Move(x.Admin,type,Input(x.Center,x.Item,3,x.DoctorId,"Corrección",direction));Assert.Equal(expected,await Stock(x.Admin,x.Center,x.Item));
    }
    [Fact] public async Task AdjustmentRequiresReason() {var x=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PostAsJsonAsync(Base+"/movements/adjustment",Input(x.Center,x.Item,2,null,null,"INCREASE"))).StatusCode);}
    [Theory][InlineData(0)][InlineData(-1)][InlineData(0.0001)]
    public async Task InvalidQuantityRejected(decimal qty){var x=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PostAsJsonAsync(Base+"/movements/entry",Input(x.Center,x.Item,qty))).StatusCode);}
    [Fact] public async Task MultilineRollbackAndSuccess() {
        var x=await Setup();var y=await Setup();await Move(x.Admin,"entry",Input(x.Center,x.Item,10));
        var before=(await x.Admin.GetFromJsonAsync<MovementPage>(Base+"/movements?centerId="+x.Center))!.Total;
        var input=new MovementInput(x.Center,x.DoctorId,Guid.NewGuid(),[new(x.Item,3),new(y.Item,1)],null);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/movements/withdrawal",input)).StatusCode);
        Assert.Equal(10,await Stock(x.Admin,x.Center,x.Item));
        Assert.Equal(before,(await x.Admin.GetFromJsonAsync<MovementPage>(Base+"/movements?centerId="+x.Center))!.Total);
        await Move(x.Admin,"entry",input with{RecipientDoctorProfileId=null,IdempotencyKey=Guid.NewGuid()});
        Assert.Equal(13,await Stock(x.Admin,x.Center,x.Item));Assert.Equal(1,await Stock(x.Admin,x.Center,y.Item));
    }
    [Fact] public async Task ConcurrentWithdrawalsCannotOversell() {
        var x=await Setup();await Move(x.Admin,"entry",Input(x.Center,x.Item,5));
        var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>x.Admin.PostAsJsonAsync(Base+"/movements/withdrawal",Input(x.Center,x.Item,4,x.DoctorId))));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(1,await Stock(x.Admin,x.Center,x.Item));
    }
    [Fact] public async Task ConcurrentRetryIsIdempotent() {
        var x=await Setup();var request=Input(x.Center,x.Item,5);
        var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>x.Admin.PostAsJsonAsync(Base+"/movements/entry",request)));
        Assert.All(responses,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));Assert.Equal(5,await Stock(x.Admin,x.Center,x.Item));
        Assert.Equal(await responses[0].Content.ReadAsStringAsync(),await responses[1].Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/movements/entry",request with{Lines=[new(x.Item,6)]})).StatusCode);
    }
    [Fact] public async Task DoctorCannotImpersonateOrUseOtherBranchOrAdminEndpoints() {
        var x=await Setup();var y=await Setup();
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Doctor.PostAsJsonAsync("/api/doctor/inventory/withdraw",new{centerId=x.Center,recipientDoctorProfileId=y.DoctorId,idempotencyKey=Guid.NewGuid(),lines=new[]{new LineInput(x.Item,1)}})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await x.Doctor.PostAsJsonAsync("/api/doctor/inventory/withdraw",new DoctorWithdrawalInput(y.Center,Guid.NewGuid(),[new(y.Item,1)],null))).StatusCode);
        foreach(var route in new[]{"/options","/items","/stocks","/movements"})Assert.Equal(HttpStatusCode.Forbidden,(await x.Doctor.GetAsync(Base+route)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await x.Doctor.PostAsJsonAsync(Base+"/movements/entry",Input(x.Center,x.Item,1))).StatusCode);
        Assert.Empty((await x.Doctor.GetFromJsonAsync<StockDto[]>("/api/doctor/inventory?centerId="+y.Center))!);
        Assert.Empty((await y.Doctor.GetFromJsonAsync<MovementPage>("/api/doctor/inventory/my-withdrawals"))!.Items);
    }
    [Fact] public async Task StockSeparatedByCenterAndInactiveItemsBlocked() {
        var x=await Setup();var y=await Setup();await Move(x.Admin,"entry",Input(x.Center,x.Item,5));Assert.Equal(0,await Stock(x.Admin,y.Center,x.Item));
        var item=(await x.Admin.GetFromJsonAsync<ItemDto[]>(Base+"/items"))!.Single(i=>i.Id==x.Item);
        Assert.Equal(HttpStatusCode.OK,(await x.Admin.PutAsJsonAsync(Base+"/items/"+x.Item,new ItemInput(item.CategoryId,item.Code,item.Name,null,item.Unit,5,false))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PostAsJsonAsync(Base+"/movements/withdrawal",Input(x.Center,x.Item,1,x.DoctorId))).StatusCode);
    }
    [Theory][InlineData(0,"OUT_OF_STOCK")][InlineData(5,"LOW_STOCK")][InlineData(6,"AVAILABLE")]
    public async Task StatusDerived(decimal qty,string status) {var x=await Setup();if(qty>0)await Move(x.Admin,"entry",Input(x.Center,x.Item,qty));Assert.Equal(status,(await x.Admin.GetFromJsonAsync<StockDto[]>(Base+"/stocks?centerId="+x.Center))!.Single(i=>i.InventoryItemId==x.Item).Status);}
    [Fact] public async Task DatabaseIntegrityAndImmutableHistory() {
        var x=await Setup();var id=await Move(x.Admin,"entry",Input(x.Center,x.Item,5));
        Assert.Equal(HttpStatusCode.MethodNotAllowed,(await x.Admin.DeleteAsync(Base+"/movements/entry")).StatusCode);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var types=db.Model.GetEntityTypes().Where(t=>t.ClrType.Namespace=="ClinicaRamirez.Api.Domain.Inventory").ToArray();
        Assert.Equal(7,types.Length);Assert.Equal(12,types.SelectMany(t=>t.GetForeignKeys()).Count());
        Assert.All(types.SelectMany(t=>t.GetForeignKeys()),fk=>Assert.Equal(DeleteBehavior.Restrict,fk.DeleteBehavior));
        Assert.Equal(12,await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_constraint WHERE contype='f' AND conrelid IN (SELECT oid FROM pg_class WHERE relname LIKE 'Inventory%') AND confdeltype='r'").SingleAsync());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"InventoryStocks\" SET \"QuantityOnHand\"=-1 WHERE \"InventoryItemId\"={x.Item}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"InventoryMovements\" SET \"Notes\"='Changed' WHERE \"Id\"={id}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"InventoryMovementLines\" WHERE \"InventoryMovementId\"={id}"));
        Assert.Equal(5,await Stock(x.Admin,x.Center,x.Item));
    }

    async Task<CreateItemWithStockInput> InitialRequest(Guid center,bool lots=false,bool expiration=false) {
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var category=await db.Set<InventoryCategory>().Select(x=>x.Id).FirstAsync();
        return new(new(category,Guid.NewGuid().ToString("N"),"Initial stock",null,"UNIT",2,true,lots,expiration),
            new(center,20,lots?"REAL-LOT":null,expiration?new DateOnly(2035,1,1):null),Guid.NewGuid());
    }
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(true,true)]
    public async Task InitialEntryIsAtomicAndIdempotent(bool lots,bool expiration) {
        var x=await Setup();var input=await InitialRequest(x.Center,lots,expiration);
        var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>x.Admin.PostAsJsonAsync(Base+"/items/with-initial-stock",input)));
        Assert.All(responses,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));
        var id=await responses[0].Content.ReadFromJsonAsync<Guid>();
        Assert.Equal(id,await responses[1].Content.ReadFromJsonAsync<Guid>());
        Assert.Equal(20,await Stock(x.Admin,x.Center,id));
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var movement=await db.Set<InventoryMovement>().SingleAsync(m=>m.IdempotencyKey==input.IdempotencyKey);
        Assert.Equal("ENTRY",movement.MovementType);
        var line=await db.Set<InventoryMovementLine>().SingleAsync(l=>l.InventoryMovementId==movement.Id);
        Assert.Equal(id,line.InventoryItemId);Assert.Equal(20,line.Quantity);
        Assert.Equal(lots,line.InventoryLotId!=null);
        Assert.Equal(lots?1:0,await db.Set<InventoryLot>().CountAsync(l=>l.InventoryItemId==id));
        if(lots)Assert.Equal(20,await db.Set<InventoryLotStock>().Where(l=>l.InventoryLotId==line.InventoryLotId).SumAsync(l=>l.QuantityOnHand));
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/items/with-initial-stock",input with{InitialStock=input.InitialStock with{Quantity=21}})).StatusCode);
    }
    [Theory][InlineData("zero")][InlineData("negative")][InlineData("precision")][InlineData("center")][InlineData("lot")][InlineData("expiration")][InlineData("inactive")][InlineData("key")]
    public async Task InitialEntryFailureRollsBackCatalogStockAndAudit(string failure) {
        var x=await Setup();var input=await InitialRequest(x.Center,true,true);
        using var auditScope=f.Services.CreateScope();var auditDb=auditScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auditCount=await auditDb.Set<AuditLog>().CountAsync();
        input=failure switch{
            "zero"=>input with{InitialStock=input.InitialStock with{Quantity=0}},
            "negative"=>input with{InitialStock=input.InitialStock with{Quantity=-1}},
            "precision"=>input with{InitialStock=input.InitialStock with{Quantity=0.0001m}},
            "center"=>input with{InitialStock=input.InitialStock with{CenterId=Guid.Empty}},
            "lot"=>input with{InitialStock=input.InitialStock with{BatchNumber=null}},
            "expiration"=>input with{InitialStock=input.InitialStock with{ExpirationDate=null}},
            "inactive"=>input with{Item=input.Item with{IsActive=false}},
            _=>input with{IdempotencyKey=Guid.Empty}
        };
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PostAsJsonAsync(Base+"/items/with-initial-stock",input)).StatusCode);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Set<InventoryItem>().AnyAsync(i=>i.Code==input.Item.Code.ToUpperInvariant()));
        Assert.False(await db.Set<InventoryMovement>().AnyAsync(m=>m.IdempotencyKey==input.IdempotencyKey));
        Assert.Equal(auditCount,await db.Set<AuditLog>().CountAsync());
    }
    [Fact] public async Task InitialEntryDoesNotExpandDoctorPermissions(){
        var x=await Setup();var input=await InitialRequest(x.Center);
        Assert.Equal(HttpStatusCode.Forbidden,(await x.Doctor.PostAsJsonAsync(Base+"/items/with-initial-stock",input)).StatusCode);
    }

    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(true,true)]
    public async Task PackagesConvertToStockUnitsAndWithdrawInStockUnits(bool lots,bool expiration) {
        var x=await Setup();var original=await InitialRequest(x.Center,lots,expiration);
        var input=original with {Item=original.Item with{PackageUnit="BOX",UnitsPerPackage=100},InitialStock=original.InitialStock with{Quantity=2,QuantityInPackages=true}};
        var response=await x.Admin.PostAsJsonAsync(Base+"/items/with-initial-stock",input);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);var id=await response.Content.ReadFromJsonAsync<Guid>();
        Assert.Equal(200,await Stock(x.Admin,x.Center,id));
        Assert.Equal(id,await (await x.Admin.PostAsJsonAsync(Base+"/items/with-initial-stock",input)).Content.ReadFromJsonAsync<Guid>());
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(200,await db.Set<InventoryMovementLine>().Where(l=>l.InventoryItemId==id).SumAsync(l=>l.Quantity));
        if(lots)Assert.Equal(200,await (from ls in db.Set<InventoryLotStock>() join l in db.Set<InventoryLot>() on ls.InventoryLotId equals l.Id where l.InventoryItemId==id select ls.QuantityOnHand).SumAsync());
        var withdrawal=new DoctorWithdrawalInput(x.Center,Guid.NewGuid(),[new(id,10)],null);
        Assert.Equal(HttpStatusCode.OK,(await x.Doctor.PostAsJsonAsync("/api/doctor/inventory/withdraw",withdrawal)).StatusCode);
        Assert.Equal(190,await Stock(x.Admin,x.Center,id));
        var dto=(await x.Admin.GetFromJsonAsync<ItemDto[]>(Base+"/items"))!.Single(i=>i.Id==id);
        Assert.True(dto.HasMovements);Assert.Equal("UNIT",dto.Unit);Assert.Equal("BOX",dto.PackageUnit);Assert.Equal(100,dto.UnitsPerPackage);
    }
    [Theory][InlineData("unit")][InlineData("package")][InlineData("factor")]
    public async Task MovementHistoryPreventsStructuralConversionChanges(string change) {
        var x=await Setup();var original=await InitialRequest(x.Center);
        var input=original with{Item=original.Item with{PackageUnit="BOX",UnitsPerPackage=100},InitialStock=original.InitialStock with{Quantity=2,QuantityInPackages=true}};
        var response=await x.Admin.PostAsJsonAsync(Base+"/items/with-initial-stock",input);response.EnsureSuccessStatusCode();var id=await response.Content.ReadFromJsonAsync<Guid>();
        var edit=change switch{"unit"=>input.Item with{Unit="BOX"},"package"=>input.Item with{PackageUnit="BAG"},_=>input.Item with{UnitsPerPackage=50}};
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PutAsJsonAsync(Base+"/items/"+id,edit)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await x.Admin.PutAsJsonAsync(Base+"/items/"+id,input.Item with{Name="Renamed"})).StatusCode);
        Assert.Equal(200,await Stock(x.Admin,x.Center,id));
    }
    [Theory][InlineData("zero")][InlineData("negative")][InlineData("precision")][InlineData("missing")][InlineData("unknown")][InlineData("fractional-packages")][InlineData("overflow")]
    public async Task InvalidPackageConversionRollsBack(string error) {
        var x=await Setup();var original=await InitialRequest(x.Center);
        var item=original.Item with{PackageUnit="BOX",UnitsPerPackage=100};
        item=error switch{"zero"=>item with{UnitsPerPackage=0},"negative"=>item with{UnitsPerPackage=-1},"precision"=>item with{UnitsPerPackage=0.0001m},"missing"=>item with{UnitsPerPackage=null},"unknown"=>item with{PackageUnit="UNKNOWN"},"overflow"=>item with{UnitsPerPackage=99999999999m},_=>item};
        var input=original with{Item=item,InitialStock=original.InitialStock with{Quantity=error=="fractional-packages"?1.5m:2m,QuantityInPackages=true}};
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PostAsJsonAsync(Base+"/items/with-initial-stock",input)).StatusCode);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Set<InventoryItem>().AnyAsync(i=>i.Code==item.Code.ToUpperInvariant()));
        Assert.False(await db.Set<InventoryMovement>().AnyAsync(m=>m.IdempotencyKey==input.IdempotencyKey));
    }
    [Fact] public async Task PackagingCanChangeBeforeMovementsAndDirectQuantityRemainsDirect() {
        var x=await Setup();var original=await InitialRequest(x.Center);
        var item=original.Item with{PackageUnit="BOX",UnitsPerPackage=100};
        var created=await x.Admin.PostAsJsonAsync(Base+"/items",item);created.EnsureSuccessStatusCode();var id=await created.Content.ReadFromJsonAsync<Guid>();
        Assert.Equal(HttpStatusCode.OK,(await x.Admin.PutAsJsonAsync(Base+"/items/"+id,item with{UnitsPerPackage=50})).StatusCode);
        var direct=original with{Item=item with{Code=Guid.NewGuid().ToString("N")},InitialStock=original.InitialStock with{Quantity=12}};
        var result=await x.Admin.PostAsJsonAsync(Base+"/items/with-initial-stock",direct);result.EnsureSuccessStatusCode();
        Assert.Equal(12,await Stock(x.Admin,x.Center,await result.Content.ReadFromJsonAsync<Guid>()));
    }
}
