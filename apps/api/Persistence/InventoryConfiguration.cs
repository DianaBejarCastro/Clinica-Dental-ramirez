using ClinicaRamirez.Api.Domain.Inventory;
using ClinicaRamirez.Api.Domain.Identity;
using ClinicaRamirez.Api.Domain.ClinicAdministration;
using Microsoft.EntityFrameworkCore;
namespace ClinicaRamirez.Api.Persistence;
public static class InventoryConfiguration
{
    public static void ConfigureInventory(this ModelBuilder b)
    {
        b.Entity<InventoryCategory>(e => {
            e.ToTable("InventoryCategories");
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Code).HasMaxLength(50).IsRequired();
            e.Property(x=>x.Name).HasMaxLength(150).IsRequired();
            e.Property(x=>x.Description).HasMaxLength(1000);
            e.Property(x=>x.IsActive).HasDefaultValue(true);
            e.Property(x=>x.DisplayOrder).HasDefaultValue(0);
            e.HasIndex(x=>x.Code).IsUnique();
            string[] codes=["ANESTHESIA","RESTORATIVE","ENDODONTICS","PERIODONTICS","ORTHODONTICS","PROTECTION","DISINFECTION","INSTRUMENTS","IMPRESSION","SURGERY","GENERAL"];
            string[] names=["Anestesia","Restauración","Endodoncia","Periodoncia","Ortodoncia","Protección","Desinfección","Instrumental","Impresión","Cirugía","General"];
            for(var i=0;i<codes.Length;i++) e.HasData(new InventoryCategory {
                Id=Guid.Parse($"82000000-0000-0000-0000-{i+1:000000000000}"),Code=codes[i],Name=names[i],DisplayOrder=i,
                CreatedAt=new DateTime(2026,10,23,0,0,0,DateTimeKind.Utc),UpdatedAt=new DateTime(2026,10,23,0,0,0,DateTimeKind.Utc) });
        });
        b.Entity<InventoryItem>(e => {
            e.ToTable("InventoryItems",t=>{
                t.HasCheckConstraint("CK_InventoryItems_Unit","\"Unit\" IN ('UNIT','BOX','PACKAGE','BOTTLE','SYRINGE','ROLL','PAIR','KIT','BAG','TUBE')");
                t.HasCheckConstraint("CK_InventoryItems_Tracking", "NOT \"TracksExpiration\" OR \"TracksLots\"");
                t.HasCheckConstraint("CK_InventoryItems_Package", "(\"PackageUnit\" IS NULL AND \"UnitsPerPackage\" IS NULL) OR (\"PackageUnit\" IS NOT NULL AND \"PackageUnit\" IN ('UNIT','BOX','PACKAGE','BOTTLE','SYRINGE','ROLL','PAIR','KIT','BAG','TUBE') AND \"UnitsPerPackage\" IS NOT NULL AND \"UnitsPerPackage\" > 0)");
                t.HasCheckConstraint("CK_InventoryItems_Minimum","\"MinimumStock\" >= 0");
            });
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Code).HasMaxLength(50).IsRequired();
            e.Property(x=>x.Name).HasMaxLength(150).IsRequired();
            e.Property(x=>x.Description).HasMaxLength(1000);
            e.Property(x=>x.Unit).HasMaxLength(20).IsRequired();
            e.Property(x=>x.PackageUnit).HasMaxLength(20);
            e.Property(x=>x.UnitsPerPackage).HasPrecision(14,3);
            e.Property(x=>x.TracksLots).HasDefaultValue(false);
            e.Property(x=>x.TracksExpiration).HasDefaultValue(false);
            e.Property(x=>x.MinimumStock).HasPrecision(14,3).HasDefaultValue(0m);
            e.Property(x=>x.IsActive).HasDefaultValue(true);
            e.HasIndex(x=>x.Code).IsUnique();
            e.HasOne<InventoryCategory>().WithMany().HasForeignKey(x=>x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<InventoryStock>(e => {
            e.ToTable("InventoryStocks",t=>t.HasCheckConstraint("CK_InventoryStocks_NonNegative","\"QuantityOnHand\" >= 0"));
            e.HasKey(x=>new{x.InventoryItemId,x.CenterId});
            e.Property(x=>x.QuantityOnHand).HasPrecision(14,3).HasDefaultValue(0m);
            e.HasOne<InventoryItem>().WithMany().HasForeignKey(x=>x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Center>().WithMany().HasForeignKey(x=>x.CenterId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<InventoryMovement>(e => {
            e.ToTable("InventoryMovements",t=>{
                t.HasCheckConstraint("CK_InventoryMovements_Type","\"MovementType\" IN ('ENTRY','WITHDRAWAL','RETURN','ADJUSTMENT')");
                t.HasCheckConstraint("CK_InventoryMovements_Adjustment","(\"MovementType\" = 'ADJUSTMENT' AND \"AdjustmentDirection\" IS NOT NULL AND \"AdjustmentDirection\" IN ('INCREASE','DECREASE') AND \"Notes\" IS NOT NULL AND length(btrim(\"Notes\")) > 0) OR (\"MovementType\" <> 'ADJUSTMENT' AND \"AdjustmentDirection\" IS NULL)");
                t.HasCheckConstraint("CK_InventoryMovements_Recipient","\"MovementType\" <> 'WITHDRAWAL' OR \"RecipientDoctorProfileId\" IS NOT NULL");
            });
            e.HasKey(x=>x.Id);
            e.Property(x=>x.RequestFingerprint).HasMaxLength(64);
            e.Property(x=>x.MovementType).HasMaxLength(20).IsRequired();
            e.Property(x=>x.AdjustmentDirection).HasMaxLength(10);
            e.HasIndex(x=>new{x.CreatedByUserId,x.IdempotencyKey}).IsUnique();
            e.HasIndex(x=>x.OccurredAt);
            e.HasIndex(x=>x.MovementType);
            e.HasOne<Center>().WithMany().HasForeignKey(x=>x.CenterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x=>x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<DoctorProfile>().WithMany().HasForeignKey(x=>x.RecipientDoctorProfileId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<InventoryMovementLine>(e => {
            e.ToTable("InventoryMovementLines",t=>t.HasCheckConstraint("CK_InventoryMovementLines_Positive","\"Quantity\" > 0"));
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Quantity).HasPrecision(14,3);
            e.HasIndex(x=>new{x.InventoryMovementId,x.InventoryItemId});
            e.HasOne<InventoryLot>().WithMany().HasForeignKey(x=>x.InventoryLotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<InventoryMovement>().WithMany().HasForeignKey(x=>x.InventoryMovementId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<InventoryItem>().WithMany().HasForeignKey(x=>x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<InventoryLot>(e => {
            e.ToTable("InventoryLots",t=>t.HasCheckConstraint("CK_InventoryLots_Dates", "\"ManufacturedAt\" IS NULL OR \"ExpirationDate\" IS NULL OR \"ManufacturedAt\" <= \"ExpirationDate\""));
            e.HasKey(x=>x.Id);
            e.Property(x=>x.BatchNumber).HasMaxLength(100).IsRequired();
            e.Property(x=>x.SupplierName).HasMaxLength(200);
            e.HasIndex(x=>new{x.InventoryItemId,x.BatchNumber}).IsUnique();
            e.HasIndex(x=>x.ExpirationDate);
            e.HasOne<InventoryItem>().WithMany().HasForeignKey(x=>x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<InventoryLotStock>(e => {
            e.ToTable("InventoryLotStocks",t=>t.HasCheckConstraint("CK_InventoryLotStocks_NonNegative", "\"QuantityOnHand\" >= 0"));
            e.HasKey(x=>x.Id);
            e.Property(x=>x.QuantityOnHand).HasPrecision(14,3);
            e.HasIndex(x=>new{x.InventoryLotId,x.CenterId}).IsUnique();
            e.HasOne<InventoryLot>().WithMany().HasForeignKey(x=>x.InventoryLotId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Center>().WithMany().HasForeignKey(x=>x.CenterId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
