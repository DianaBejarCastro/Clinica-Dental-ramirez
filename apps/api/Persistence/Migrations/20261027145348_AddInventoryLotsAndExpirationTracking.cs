using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryLotsAndExpirationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InventoryMovementLines_InventoryMovementId_InventoryItemId",
                table: "InventoryMovementLines");

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "InventoryMovements",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InventoryLotId",
                table: "InventoryMovementLines",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TracksExpiration",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "TracksLots",
                table: "InventoryItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "InventoryLots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ManufacturedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLots", x => x.Id);
                    table.CheckConstraint("CK_InventoryLots_Dates", "\"ManufacturedAt\" IS NULL OR \"ExpirationDate\" IS NULL OR \"ManufacturedAt\" <= \"ExpirationDate\"");
                    table.ForeignKey(
                        name: "FK_InventoryLots_InventoryItems_InventoryItemId",
                        column: x => x.InventoryItemId,
                        principalTable: "InventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InventoryLotStocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryLotId = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(14,3)", precision: 14, scale: 3, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryLotStocks", x => x.Id);
                    table.CheckConstraint("CK_InventoryLotStocks_NonNegative", "\"QuantityOnHand\" >= 0");
                    table.ForeignKey(
                        name: "FK_InventoryLotStocks_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryLotStocks_InventoryLots_InventoryLotId",
                        column: x => x.InventoryLotId,
                        principalTable: "InventoryLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementLines_InventoryLotId",
                table: "InventoryMovementLines",
                column: "InventoryLotId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementLines_InventoryMovementId_InventoryItemId",
                table: "InventoryMovementLines",
                columns: new[] { "InventoryMovementId", "InventoryItemId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItems_Tracking",
                table: "InventoryItems",
                sql: "NOT \"TracksExpiration\" OR \"TracksLots\"");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_ExpirationDate",
                table: "InventoryLots",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLots_InventoryItemId_BatchNumber",
                table: "InventoryLots",
                columns: new[] { "InventoryItemId", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLotStocks_CenterId",
                table: "InventoryLotStocks",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryLotStocks_InventoryLotId_CenterId",
                table: "InventoryLotStocks",
                columns: new[] { "InventoryLotId", "CenterId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryMovementLines_InventoryLots_InventoryLotId",
                table: "InventoryMovementLines",
                column: "InventoryLotId",
                principalTable: "InventoryLots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryMovementLines_InventoryLots_InventoryLotId",
                table: "InventoryMovementLines");

            migrationBuilder.DropTable(
                name: "InventoryLotStocks");

            migrationBuilder.DropTable(
                name: "InventoryLots");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovementLines_InventoryLotId",
                table: "InventoryMovementLines");

            migrationBuilder.DropIndex(
                name: "IX_InventoryMovementLines_InventoryMovementId_InventoryItemId",
                table: "InventoryMovementLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItems_Tracking",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "InventoryMovements");

            migrationBuilder.DropColumn(
                name: "InventoryLotId",
                table: "InventoryMovementLines");

            migrationBuilder.DropColumn(
                name: "TracksExpiration",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "TracksLots",
                table: "InventoryItems");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovementLines_InventoryMovementId_InventoryItemId",
                table: "InventoryMovementLines",
                columns: new[] { "InventoryMovementId", "InventoryItemId" },
                unique: true);
        }
    }
}
