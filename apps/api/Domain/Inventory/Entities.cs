namespace ClinicaRamirez.Api.Domain.Inventory;

public sealed class InventoryCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class InventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Unit { get; set; } = "UNIT";
    public string? PackageUnit { get; set; }
    public decimal? UnitsPerPackage { get; set; }
    public decimal MinimumStock { get; set; }
    public bool TracksLots { get; set; }
    public bool TracksExpiration { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class InventoryStock
{
    public Guid InventoryItemId { get; set; }
    public Guid CenterId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CenterId { get; set; }
    public string MovementType { get; set; } = "";
    public Guid CreatedByUserId { get; set; }
    public Guid? RecipientDoctorProfileId { get; set; }
    // ADJUSTMENT uses INCREASE / DECREASE; line quantities remain positive.
    public string? AdjustmentDirection { get; set; }
    public Guid IdempotencyKey { get; set; }
    public string? RequestFingerprint { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class InventoryMovementLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InventoryMovementId { get; set; }
    public Guid InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public Guid? InventoryLotId { get; set; }
}

public sealed class InventoryLot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InventoryItemId { get; set; }
    public string BatchNumber { get; set; } = "";
    public DateOnly? ExpirationDate { get; set; }
    public DateOnly? ManufacturedAt { get; set; }
    public string? SupplierName { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class InventoryLotStock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InventoryLotId { get; set; }
    public Guid CenterId { get; set; }
    public decimal QuantityOnHand { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
