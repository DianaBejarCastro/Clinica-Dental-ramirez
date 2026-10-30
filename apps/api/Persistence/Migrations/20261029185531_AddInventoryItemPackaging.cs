using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryItemPackaging : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PackageUnit",
                table: "InventoryItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitsPerPackage",
                table: "InventoryItems",
                type: "numeric(14,3)",
                precision: 14,
                scale: 3,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryItems_Package",
                table: "InventoryItems",
                sql: "(\"PackageUnit\" IS NULL AND \"UnitsPerPackage\" IS NULL) OR (\"PackageUnit\" IS NOT NULL AND \"PackageUnit\" IN ('UNIT','BOX','PACKAGE','BOTTLE','SYRINGE','ROLL','PAIR','KIT','BAG','TUBE') AND \"UnitsPerPackage\" IS NOT NULL AND \"UnitsPerPackage\" > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryItems_Package",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "PackageUnit",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "UnitsPerPackage",
                table: "InventoryItems");
        }
    }
}
