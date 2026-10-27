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
public sealed class InventoryLotsTests(IdentityFixture f):IClassFixture<IdentityFixture>
{
    const string Base="/api/admin/inventory";
    async Task<HttpClient> Login(string? email=null) {var c=f.Client();var s=await f.Login(c,email);c.DefaultRequestHeaders.Authorization=new("Bearer",s.Body.AccessToken);return c;}
    async Task<(HttpClient Admin,HttpClient Doctor,Guid DoctorId,Guid Center,Guid Item)> Setup(bool tracking=true,bool expiration=true) {
        var admin=await Login();var email=await f.CreateUser();
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var u=await db.Users.SingleAsync(x=>x.Email==email);var role=await db.Roles.SingleAsync(x=>x.Code=="DOCTOR");
        var d=new DoctorProfile{UserId=u.Id,ProfessionalLicense=Guid.NewGuid().ToString("N").ToUpperInvariant()};
        var c=new Center{ClinicId=Clinic.SingletonId,Code="INV_"+Guid.NewGuid().ToString("N").ToUpperInvariant(),Name="Inventario prueba",Address="Prueba"};
        db.AddRange(d,c,new UserRole{UserId=u.Id,RoleId=role.Id},new DoctorCenter{DoctorId=d.Id,CenterId=c.Id,IsActive=true});
        await db.SaveChangesAsync();
        var category=await db.Set<InventoryCategory>().Select(x=>x.Id).FirstAsync();
        var result=await admin.PostAsJsonAsync(Base+"/items",new ItemInput(category,Guid.NewGuid().ToString("N"),"Guantes","Test","BOX",5,true,tracking,tracking&&expiration));
        Assert.Equal(HttpStatusCode.OK,result.StatusCode);
        return(admin,await Login(email),d.Id,c.Id,await result.Content.ReadFromJsonAsync<Guid>());
    }
    static MovementInput Input(Guid center,Guid item,decimal qty,Guid? doctor=null,string? notes=null,string? direction=null)=>new(center,doctor,Guid.NewGuid(),[new(item,qty)],notes,direction);
    async Task<decimal> Stock(HttpClient c,Guid center,Guid item)=> (await c.GetFromJsonAsync<StockDto[]>(Base+"/stocks?centerId="+center))!.Single(x=>x.InventoryItemId==item).QuantityOnHand;
    async Task<Guid> Move(HttpClient c,string type,MovementInput input) {var r=await c.PostAsJsonAsync(Base+"/movements/"+type,input);Assert.Equal(HttpStatusCode.OK,r.StatusCode);return await r.Content.ReadFromJsonAsync<Guid>();}

    static DateOnly Today=>ClinicaRamirez.Api.Application.Inventory.InventoryLotService.Today;
    static LineInput Batch(Guid item,decimal qty,string number,int? days=30)=>new(item,qty,null,number,days is null?null:Today.AddDays(days.Value));
    async Task<LotDto[]> Lots(HttpClient c,Guid item)=> (await c.GetFromJsonAsync<LotDto[]>(Base+"/lots?itemId="+item))!;
    async Task<Guid> Entry(HttpClient c,Guid center,LineInput line)=>await Move(c,"entry",new(center,null,Guid.NewGuid(),[line],null));
    async Task Consistent(Guid item) {
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach(var s in await db.Set<InventoryStock>().Where(x=>x.InventoryItemId==item).ToArrayAsync()) {
            var sum=await (from b in db.Set<InventoryLotStock>() join l in db.Set<InventoryLot>() on b.InventoryLotId equals l.Id where l.InventoryItemId==item&&b.CenterId==s.CenterId select b.QuantityOnHand).SumAsync();
            Assert.Equal(s.QuantityOnHand,sum);Assert.True(sum>=0);
        }
    }
    [Fact] public async Task FefoMultilotDoctorReturnAdjustmentAndTraceability() {
        var x=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,10,"A",20));await Entry(x.Admin,x.Center,Batch(x.Item,10,"B",90));
        var input=new DoctorWithdrawalInput(x.Center,Guid.NewGuid(),[new(x.Item,12)],null);
        var r=await x.Doctor.PostAsJsonAsync("/api/doctor/inventory/withdraw",input);Assert.Equal(HttpStatusCode.OK,r.StatusCode);
        var history=(await x.Doctor.GetFromJsonAsync<MovementPage>("/api/doctor/inventory/my-withdrawals"))!.Items.Single();
        Assert.Equal(2,history.Lines.Length);Assert.Equal(10,history.Lines.Single(l=>l.BatchNumber=="A").Quantity);Assert.Equal(2,history.Lines.Single(l=>l.BatchNumber=="B").Quantity);
        var batches=await Lots(x.Admin,x.Item);Assert.Equal(0,batches.Single(l=>l.BatchNumber=="A").QuantityOnHand);Assert.Equal(8,batches.Single(l=>l.BatchNumber=="B").QuantityOnHand);
        Assert.Equal(8,await Stock(x.Admin,x.Center,x.Item));
        Assert.Equal(await r.Content.ReadAsStringAsync(),await (await x.Doctor.PostAsJsonAsync("/api/doctor/inventory/withdraw",input)).Content.ReadAsStringAsync());
        await Move(x.Admin,"return",new(x.Center,x.DoctorId,Guid.NewGuid(),[new(x.Item,2,batches.Single(l=>l.BatchNumber=="A").Id)],"Devolución"));
        await Move(x.Admin,"adjustment",new(x.Center,null,Guid.NewGuid(),[new(x.Item,1,batches.Single(l=>l.BatchNumber=="B").Id)],"Conteo físico","DECREASE"));
        Assert.Equal(9,await Stock(x.Admin,x.Center,x.Item));await Consistent(x.Item);
    }
    [Fact] public async Task ReusedBatchAndConcurrentIdempotency() {
        var x=await Setup();var input=new MovementInput(x.Center,null,Guid.NewGuid(),[Batch(x.Item,5,"A")],null);
        var results=await Task.WhenAll(x.Admin.PostAsJsonAsync(Base+"/movements/entry",input),x.Admin.PostAsJsonAsync(Base+"/movements/entry",input));
        Assert.All(results,r=>Assert.Equal(HttpStatusCode.OK,r.StatusCode));Assert.Single(await Lots(x.Admin,x.Item));Assert.Equal(5,await Stock(x.Admin,x.Center,x.Item));
        await Entry(x.Admin,x.Center,Batch(x.Item,2,"A"));Assert.Single(await Lots(x.Admin,x.Item));Assert.Equal(7,await Stock(x.Admin,x.Center,x.Item));
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/movements/entry",input with{Lines=[Batch(x.Item,5,"OTHER")]})).StatusCode);await Consistent(x.Item);
    }
    [Theory][InlineData(true)][InlineData(false)] public async Task ConcurrentFefoOrFifoCannotOversell(bool expiration) {
        var x=await Setup(true,expiration);await Entry(x.Admin,x.Center,Batch(x.Item,5,"A",expiration?30:null));
        var results=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>x.Doctor.PostAsJsonAsync("/api/doctor/inventory/withdraw",new DoctorWithdrawalInput(x.Center,Guid.NewGuid(),[new(x.Item,4)],null))));
        Assert.Single(results,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);Assert.Equal(1,await Stock(x.Admin,x.Center,x.Item));await Consistent(x.Item);
    }
    [Fact] public async Task ExpiredAndInactiveAreNeverSelectedAndFailureRollsBack() {
        var x=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,10,"EXPIRED",-1));await Entry(x.Admin,x.Center,Batch(x.Item,2,"VALID",2));
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/movements/withdrawal",Input(x.Center,x.Item,3,x.DoctorId))).StatusCode);
        Assert.Equal(12,await Stock(x.Admin,x.Center,x.Item));Assert.Equal(2,(await Lots(x.Admin,x.Item)).Single(l=>l.BatchNumber=="VALID").QuantityOnHand);
        var lot=(await Lots(x.Admin,x.Item)).Single(l=>l.BatchNumber=="VALID");Assert.Equal(HttpStatusCode.NoContent,(await x.Admin.PutAsJsonAsync(Base+"/lots/"+lot.Id,new LotUpdate(lot.ExpirationDate,null,null,false,"Retirado por control"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/movements/withdrawal",Input(x.Center,x.Item,1,x.DoctorId))).StatusCode);await Consistent(x.Item);
    }
    [Fact] public async Task FifoUsesReceivedAtWithoutInventedExpiration() {
        var x=await Setup(true,false);await Entry(x.Admin,x.Center,Batch(x.Item,3,"Z-FIRST",null));await Entry(x.Admin,x.Center,Batch(x.Item,5,"A-SECOND",null));
        await Move(x.Admin,"withdrawal",Input(x.Center,x.Item,4,x.DoctorId));var lots=await Lots(x.Admin,x.Item);Assert.All(lots,l=>Assert.Null(l.ExpirationDate));Assert.Equal(0,lots.Single(l=>l.BatchNumber=="Z-FIRST").QuantityOnHand);Assert.Equal(4,lots.Single(l=>l.BatchNumber=="A-SECOND").QuantityOnHand);await Consistent(x.Item);
    }
    [Theory][InlineData(-1,"EXPIRED")][InlineData(0,"EXPIRING")][InlineData(30,"EXPIRING")][InlineData(31,"VALID")][InlineData(60,"VALID")][InlineData(90,"VALID")]
    public async Task CalculatedStatusesAndAlerts(int days,string status) {
        var x=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,1,"A",days));Assert.Equal(status,(await Lots(x.Admin,x.Item)).Single().Status);
        var alerts=(await x.Admin.GetFromJsonAsync<ExpirationAlerts>(Base+"/expiration-alerts?centerId="+x.Center))!;
        Assert.Equal(days<0?1:0,alerts.Expired);Assert.Equal(days>=0&&days<=30?1:0,alerts.Within30);Assert.Equal(days>=0&&days<=60?1:0,alerts.Within60);Assert.Equal(days>=0&&days<=90?1:0,alerts.Within90);
    }
    [Theory][InlineData("entry")][InlineData("return")][InlineData("adjustment")]
    public async Task TrackedMovementRequiresBatch(string type) {
        var x=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PostAsJsonAsync(Base+"/movements/"+type,Input(x.Center,x.Item,2,x.DoctorId,"Reason",type=="adjustment"?"INCREASE":null))).StatusCode);Assert.Equal(0,await Stock(x.Admin,x.Center,x.Item));
    }
    [Fact] public async Task RequiredExpirationAndCorrectProduct() {
        var x=await Setup();var y=await Setup();await Entry(y.Admin,y.Center,Batch(y.Item,2,"A"));var lot=(await Lots(y.Admin,y.Item)).Single();
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PostAsJsonAsync(Base+"/movements/entry",new MovementInput(x.Center,null,Guid.NewGuid(),[Batch(x.Item,2,"A",null)],null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PostAsJsonAsync(Base+"/movements/return",new MovementInput(x.Center,null,Guid.NewGuid(),[new(x.Item,2,lot.Id)],null))).StatusCode);await Consistent(x.Item);
    }
    [Fact] public async Task BatchIdentityUniquePerItemAndMetadataConflict() {
        var x=await Setup();var y=await Setup();var input=new LotInput(x.Item,"A",Today.AddDays(30));
        var first=await x.Admin.PostAsJsonAsync(Base+"/lots",input);var again=await x.Admin.PostAsJsonAsync(Base+"/lots",input);Assert.Equal(await first.Content.ReadAsStringAsync(),await again.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/lots",input with{ExpirationDate=Today.AddDays(40)})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await y.Admin.PostAsJsonAsync(Base+"/lots",input with{InventoryItemId=y.Item})).StatusCode);
    }
    [Fact] public async Task StockSeparatedByCenter() {
        var x=await Setup();var y=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,2,"A"));await Entry(x.Admin,y.Center,Batch(x.Item,7,"A"));Assert.Equal(2,(await Lots(x.Admin,x.Item)).Length);Assert.Equal(2,await Stock(x.Admin,x.Center,x.Item));Assert.Equal(7,await Stock(x.Admin,y.Center,x.Item));await Consistent(x.Item);
    }
    [Theory][InlineData(4,false)][InlineData(6,false)][InlineData(5,true)] public async Task InitializeExistingStockRequiresExactAllocation(decimal quantity,bool success) {
        var x=await Setup(false);await Entry(x.Admin,x.Center,new(x.Item,5));
        var item=(await x.Admin.GetFromJsonAsync<ItemDto[]>(Base+"/items"))!.Single(i=>i.Id==x.Item);
        Assert.False(item.TracksLots);Assert.False(item.TracksExpiration);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PutAsJsonAsync(Base+"/items/"+x.Item,new ItemInput(item.CategoryId,item.Code,item.Name,null,"BOX",5,true,true,true))).StatusCode);
        var input=new InitializeLotsInput(true,[new(x.Center,quantity,new(x.Item,"REAL",Today.AddDays(30)))],"Inventario físico");
        Assert.Equal(success?HttpStatusCode.NoContent:HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/items/"+x.Item+"/initialize-lots",input)).StatusCode);
        Assert.Equal(5,await Stock(x.Admin,x.Center,x.Item));
        if(success){await Consistent(x.Item);Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PutAsJsonAsync(Base+"/items/"+x.Item,new ItemInput(item.CategoryId,item.Code,item.Name,null,"BOX",5))).StatusCode);}
        else Assert.Empty(await Lots(x.Admin,x.Item));
    }
    [Fact] public async Task DoctorCannotManageLotsAndAdminCorrectionsAudited() {
        var x=await Setup();var input=new LotInput(x.Item,"A",Today.AddDays(30));
        Assert.Equal(HttpStatusCode.Forbidden,(await x.Doctor.PostAsJsonAsync(Base+"/lots",input)).StatusCode);
        await Entry(x.Admin,x.Center,Batch(x.Item,5,"A"));var lot=(await Lots(x.Admin,x.Item)).Single();
        await Move(x.Admin,"withdrawal",Input(x.Center,x.Item,1,x.DoctorId));
        Assert.Equal(HttpStatusCode.Forbidden,(await x.Doctor.PutAsJsonAsync(Base+"/lots/"+lot.Id,new LotUpdate(Today.AddDays(40),null,null,true,"Reason"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PutAsJsonAsync(Base+"/lots/"+lot.Id,new LotUpdate(Today.AddDays(40),null,null,true,""))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await x.Admin.PutAsJsonAsync(Base+"/lots/"+lot.Id,new LotUpdate(Today.AddDays(40),null,null,true,"Corrección documentada"))).StatusCode);
        await Move(x.Admin,"adjustment",new(x.Center,null,Guid.NewGuid(),[new(x.Item,1,lot.Id)],"Conteo","DECREASE"));
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();var logs=await db.AuditLogs.Where(a=>a.EntityId==lot.Id).ToArrayAsync();
        Assert.Contains(logs,a=>a.Action=="inventory_lot_created");Assert.Contains(logs,a=>a.Action=="inventory_lot_updated");Assert.Contains(logs,a=>a.Action=="inventory_lot_stock_adjusted");
        Assert.Equal(HttpStatusCode.MethodNotAllowed,(await x.Admin.DeleteAsync(Base+"/lots/"+lot.Id)).StatusCode);
    }
    [Fact] public async Task NewSchemaDateAndRestrictAndUniqueness() {
        var x=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,1,"A"));using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lotType=db.Model.FindEntityType(typeof(InventoryLot))!;Assert.Equal("date",lotType.FindProperty("ExpirationDate")!.GetColumnType());
        var fks=lotType.GetForeignKeys().Concat(db.Model.FindEntityType(typeof(InventoryLotStock))!.GetForeignKeys()).Concat(db.Model.FindEntityType(typeof(InventoryMovementLine))!.GetForeignKeys().Where(k=>k.PrincipalEntityType.ClrType==typeof(InventoryLot))).ToArray();
        Assert.Equal(4,fks.Length);Assert.All(fks,k=>Assert.Equal(DeleteBehavior.Restrict,k.DeleteBehavior));
        var lot=await db.Set<InventoryLot>().SingleAsync(l=>l.InventoryItemId==x.Item);db.Add(new InventoryLot{InventoryItemId=x.Item,BatchNumber=lot.BatchNumber});await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
    }
    [Theory][InlineData("ENTRY")][InlineData("RETURN")][InlineData("ADJUSTMENT")]
    public async Task UntrackedRejectsInventedLots(string type) {
        var x=await Setup(false);var r=await x.Admin.PostAsJsonAsync(Base+"/movements/"+type,new MovementInput(x.Center,null,Guid.NewGuid(),[Batch(x.Item,1,"FAKE")],"Reason",type=="ADJUSTMENT"?"INCREASE":null));Assert.Equal(HttpStatusCode.BadRequest,r.StatusCode);Assert.Equal(0,await Stock(x.Admin,x.Center,x.Item));
    }

    [Theory][InlineData(30,31)][InlineData(60,61)][InlineData(90,91)] public async Task AlertWindowBoundaryAndBatchSearch(int window,int later) {
        var x=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,1,"BOUNDARY",window));await Entry(x.Admin,x.Center,Batch(x.Item,1,"LATER",later));
        var found=(await x.Admin.GetFromJsonAsync<LotDto[]>(Base+"/lots?itemId="+x.Item+"&status=EXPIRING&days="+window))!;Assert.Single(found);Assert.Equal("BOUNDARY",found[0].BatchNumber);
        Assert.Single((await x.Admin.GetFromJsonAsync<LotDto[]>(Base+"/lots?itemId="+x.Item+"&search=LATER"))!);
    }
    [Theory][InlineData(1,10)][InlineData(-1,-2)] public async Task InvalidManufacturingDates(int manufactured,int expires) {
        var x=await Setup();Assert.Equal(HttpStatusCode.BadRequest,(await x.Admin.PostAsJsonAsync(Base+"/lots",new LotInput(x.Item,"BAD",Today.AddDays(expires),Today.AddDays(manufactured)))).StatusCode);
    }
    [Fact] public async Task MultilineTrackedRollbackIncludesLotAndTotal() {
        var x=await Setup();var y=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,5,"A"));
        var input=new MovementInput(x.Center,x.DoctorId,Guid.NewGuid(),[new(x.Item,4),new(y.Item,1)],null);
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/movements/withdrawal",input)).StatusCode);Assert.Equal(5,await Stock(x.Admin,x.Center,x.Item));await Consistent(x.Item);
    }
    [Fact] public async Task DoctorCannotOverrideAutomaticAllocation() {
        var x=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,5,"A"));var lot=(await Lots(x.Admin,x.Item)).Single();
        Assert.Equal(HttpStatusCode.BadRequest,(await x.Doctor.PostAsJsonAsync("/api/doctor/inventory/withdraw",new DoctorWithdrawalInput(x.Center,Guid.NewGuid(),[new(x.Item,1,lot.Id)],null))).StatusCode);Assert.Equal(5,await Stock(x.Admin,x.Center,x.Item));
    }
    [Fact] public async Task ExpirationTodayIsUsable() {
        var x=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,1,"TODAY",0));await Move(x.Admin,"withdrawal",Input(x.Center,x.Item,1,x.DoctorId));Assert.Equal(0,await Stock(x.Admin,x.Center,x.Item));await Consistent(x.Item);
    }
    [Fact] public async Task CannotReduceSpecificBatchBelowZeroEvenWithOtherStock() {
        var x=await Setup();await Entry(x.Admin,x.Center,Batch(x.Item,1,"A"));await Entry(x.Admin,x.Center,Batch(x.Item,10,"B"));var a=(await Lots(x.Admin,x.Item)).Single(l=>l.BatchNumber=="A");
        Assert.False((await x.Admin.PostAsJsonAsync(Base+"/movements/adjustment",new MovementInput(x.Center,null,Guid.NewGuid(),[new(x.Item,2,a.Id)],"Conteo","DECREASE"))).IsSuccessStatusCode);Assert.Equal(11,await Stock(x.Admin,x.Center,x.Item));await Consistent(x.Item);
    }
    [Fact] public async Task InitializationChecksEveryBranchNotJustGrandTotal() {
        var x=await Setup(false);var y=await Setup(false);await Entry(x.Admin,x.Center,new(x.Item,2));await Entry(x.Admin,y.Center,new(x.Item,3));
        var input=new InitializeLotsInput(false,[new(x.Center,3,new(x.Item,"A")),new(y.Center,2,new(x.Item,"B"))],"Conteo");
        Assert.Equal(HttpStatusCode.Conflict,(await x.Admin.PostAsJsonAsync(Base+"/items/"+x.Item+"/initialize-lots",input)).StatusCode);
        input=input with{Allocations=[new(x.Center,2,new(x.Item,"A")),new(y.Center,3,new(x.Item,"B"))]};Assert.Equal(HttpStatusCode.NoContent,(await x.Admin.PostAsJsonAsync(Base+"/items/"+x.Item+"/initialize-lots",input)).StatusCode);await Consistent(x.Item);
    }
}
