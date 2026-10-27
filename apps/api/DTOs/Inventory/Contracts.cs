using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace ClinicaRamirez.Api.DTOs.Inventory;
public sealed record CategoryInput([Required,MaxLength(50)]string Code,[Required,MaxLength(150)]string Name,[MaxLength(1000)]string? Description,bool IsActive=true,int DisplayOrder=0);
public sealed record CategoryDto(Guid Id,string Code,string Name,string? Description,bool IsActive,int DisplayOrder);
public sealed record ItemInput(Guid CategoryId,[Required,MaxLength(50)]string Code,[Required,MaxLength(150)]string Name,[MaxLength(1000)]string? Description,[Required]string Unit,decimal MinimumStock,bool IsActive=true,bool TracksLots=false,bool TracksExpiration=false);
public sealed record ItemDto(Guid Id,Guid CategoryId,string Code,string Name,string Category,string? Description,string Unit,decimal MinimumStock,bool IsActive,bool TracksLots=false,bool TracksExpiration=false);
public sealed record StockDto(Guid InventoryItemId,Guid CenterId,string Center,string Code,string Name,Guid CategoryId,string Category,string Unit,decimal QuantityOnHand,decimal MinimumStock,string Status,bool IsActive,bool TracksLots=false,bool TracksExpiration=false);
public sealed record LineInput(Guid InventoryItemId,decimal Quantity,Guid? InventoryLotId=null,string? BatchNumber=null,DateOnly? ExpirationDate=null,DateOnly? ManufacturedAt=null,string? SupplierName=null);
public sealed record MovementInput(Guid CenterId,Guid? RecipientDoctorProfileId,Guid IdempotencyKey,[Required,MinLength(1),MaxLength(100)]LineInput[] Lines,[MaxLength(2000)]string? Notes,string? AdjustmentDirection=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DoctorWithdrawalInput(Guid CenterId,Guid IdempotencyKey,[Required,MinLength(1),MaxLength(100)]LineInput[] Lines,[MaxLength(2000)]string? Notes);
public sealed record MovementLineDto(Guid InventoryItemId,string Code,string Name,string Unit,decimal Quantity,Guid? InventoryLotId=null,string? BatchNumber=null,DateOnly? ExpirationDate=null);
public sealed record MovementDto(Guid Id,Guid CenterId,string Center,string MovementType,DateTime OccurredAt,Guid CreatedByUserId,string RegisteredBy,Guid? RecipientDoctorProfileId,string? Doctor,string? ProfessionalLicense,string? Notes,string? AdjustmentDirection,MovementLineDto[] Lines);
public sealed record CenterOption(Guid Id,string Name);
public sealed record DoctorOption(Guid Id,string Name,string ProfessionalLicense,Guid[] CenterIds);
public sealed record InventoryOptions(CategoryDto[] Categories,CenterOption[] Centers,DoctorOption[] Doctors);
public sealed record MovementPage(MovementDto[] Items,int Total,int Page,int PageSize);

public sealed record LotInput(Guid InventoryItemId,[Required,MaxLength(100)]string BatchNumber,DateOnly? ExpirationDate=null,DateOnly? ManufacturedAt=null,[MaxLength(200)]string? SupplierName=null);
public sealed record LotUpdate(DateOnly? ExpirationDate,DateOnly? ManufacturedAt,[MaxLength(200)]string? SupplierName,bool IsActive,[Required,MaxLength(2000)]string Reason);
public sealed record LotAllocation(Guid CenterId,decimal Quantity,[Required]LotInput Lot);
public sealed record InitializeLotsInput(bool TracksExpiration,[Required,MinLength(1),MaxLength(100)]LotAllocation[] Allocations,[Required,MaxLength(2000)]string Reason);
public sealed record LotDto(Guid Id,Guid InventoryItemId,string Item,Guid CategoryId,string BatchNumber,Guid? CenterId,string? Center,decimal QuantityOnHand,DateTime ReceivedAt,DateOnly? ExpirationDate,DateOnly? ManufacturedAt,string? SupplierName,bool IsActive,int? DaysRemaining,string Status);
public sealed record ExpirationAlerts(int Expired,int Within30,int Within60,int Within90,int LowStock,int OutOfStock);
