using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientDoctorProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdjustmentDirection = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.CheckConstraint("CK_InventoryMovements_Adjustment", "(\"MovementType\" = 'ADJUSTMENT' AND \"AdjustmentDirection\" IS NOT NULL AND \"AdjustmentDirection\" IN ('INCREASE','DECREASE') AND \"Notes\" IS NOT NULL AND length(btrim(\"Notes\")) > 0) OR (\"MovementType\" <> 'ADJUSTMENT' AND \"AdjustmentDirection\" IS NULL)");
                    table.CheckConstraint("CK_InventoryMovements_Recipient", "\"MovementType\" <> 'WITHDRAWAL' OR \"RecipientDoctorProfileId\" IS NOT NULL");
                    table.CheckConstraint("CK_InventoryMovements_Type", "\"MovementType\" IN ('ENTRY','WITHDRAWAL','RETURN','ADJUSTMENT')");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_DoctorProfiles_RecipientDoctorProfileId",
                        column: x => x.RecipientDoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MinimumStock = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false, defaultValue: 0m),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryItems", x => x.Id);
                    table.CheckConstraint("CK_InventoryItems_Minimum", "\"MinimumStock\" >= 0");
                    table.CheckConstraint("CK_InventoryItems_Unit", "\"Unit\" IN ('UNIT','BOX','PACKAGE','BOTTLE','SYRINGE','ROLL','PAIR','KIT','BAG','TUBE')");
                    table.ForeignKey(
                        name: "FK_InventoryItems_InventoryCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "InventoryCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovementLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryMovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovementLines", x => x.Id);
                    table.CheckConstraint("CK_InventoryMovementLines_Positive", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_InventoryMovementLines_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryMovementLines_InventoryMovements_InventoryMovement~",
                        column: x => x.InventoryMovementId,
                        principalTable: "InventoryMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryStocks",
                columns: table => new
                {
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false, defaultValue: 0m),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryStocks", x => new { x.InventoryItemId, x.CenterId });
                    table.CheckConstraint("CK_InventoryStocks_NonNegative", "\"QuantityOnHand\" >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryStocks_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryStocks_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "InventoryCategories",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "IsActive", "Name", "UpdatedAt" },
                values: new object[] { new Guid("82000000-0000-0000-0000-000000000001"), "ANESTHESIA", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, true, "Anestesia", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.InsertData(
                table: "InventoryCategories",
                columns: new[] { "Id", "Code", "CreatedAt", "Description", "DisplayOrder", "IsActive", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("82000000-0000-0000-0000-000000000002"), "RESTORATIVE", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 1, true, "Restauración", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("82000000-0000-0000-0000-000000000003"), "ENDODONTICS", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 2, true, "Endodoncia", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("82000000-0000-0000-0000-000000000004"), "PERIODONTICS", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 3, true, "Periodoncia", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("82000000-0000-0000-0000-000000000005"), "ORTHODONTICS", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 4, true, "Ortodoncia", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("82000000-0000-0000-0000-000000000006"), "PROTECTION", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 5, true, "Protección", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("82000000-0000-0000-0000-000000000007"), "DISINFECTION", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 6, true, "Desinfección", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("82000000-0000-0000-0000-000000000008"), "INSTRUMENTS", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 7, true, "Instrumental", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("82000000-0000-0000-0000-000000000009"), "IMPRESSION", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 8, true, "Impresión", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("82000000-0000-0000-0000-000000000010"), "SURGERY", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 9, true, "Cirugía", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("82000000-0000-0000-0000-000000000011"), "GENERAL", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc), null, 10, true, "General", new DateTime(2026, 10, 23, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryCategories_Code",
                table: "InventoryCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_CategoryId",
                table: "InventoryItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_Code",
                table: "InventoryItems",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementLines_InventoryItemId",
                table: "InventoryMovementLines",
                column: "InventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementLines_InventoryMovementId_InventoryItemId",
                table: "InventoryMovementLines",
                columns: new[] { "InventoryMovementId", "InventoryItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CenterId",
                table: "InventoryMovements",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CreatedByUserId_IdempotencyKey",
                table: "InventoryMovements",
                columns: new[] { "CreatedByUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_MovementType",
                table: "InventoryMovements",
                column: "MovementType");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_OccurredAt",
                table: "InventoryMovements",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_RecipientDoctorProfileId",
                table: "InventoryMovements",
                column: "RecipientDoctorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStocks_CenterId",
                table: "InventoryStocks",
                column: "CenterId");

            migrationBuilder.Sql("""
                CREATE FUNCTION inventory_history_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Inventory history is immutable' USING ERRCODE='23514'; END $$;
                CREATE TRIGGER inventory_movement_immutable BEFORE UPDATE OR DELETE ON "InventoryMovements"
                FOR EACH ROW EXECUTE FUNCTION inventory_history_immutable();
                CREATE TRIGGER inventory_line_immutable BEFORE UPDATE OR DELETE ON "InventoryMovementLines"
                FOR EACH ROW EXECUTE FUNCTION inventory_history_immutable();
                INSERT INTO "Permissions" ("Id","Code","Name","Module","CreatedAt")
                SELECT gen_random_uuid(), code, code, 'inventory', now()
                FROM unnest(ARRAY['inventory.view','inventory.item.manage','inventory.movement.view','inventory.entry.create','inventory.withdrawal.create','inventory.return.create','inventory.adjustment.create','inventory.doctor.withdraw']) AS code
                ON CONFLICT ("Code") DO NOTHING;
                INSERT INTO "RolePermissions" ("RoleId","PermissionId","AssignedAt")
                SELECT r."Id",p."Id",now() FROM "Roles" r CROSS JOIN "Permissions" p
                WHERE (r."Code"='ADMINISTRATOR' AND p."Module"='inventory')
                   OR (r."Code"='DOCTOR' AND p."Code" IN ('inventory.view','inventory.doctor.withdraw'))
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER inventory_movement_immutable ON \"InventoryMovements\"; DROP TRIGGER inventory_line_immutable ON \"InventoryMovementLines\"; DROP FUNCTION inventory_history_immutable();");
            migrationBuilder.DropTable(
                name: "InventoryMovementLines");

            migrationBuilder.DropTable(
                name: "InventoryStocks");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "InventoryItems");

            migrationBuilder.DropTable(
                name: "InventoryCategories");
        }
    }
}
