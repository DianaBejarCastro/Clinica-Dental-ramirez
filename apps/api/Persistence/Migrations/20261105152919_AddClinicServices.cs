using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ShortDescription = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    VisualVariant = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicServices", x => x.Id);
                    table.CheckConstraint("CK_ClinicServices_Name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_ClinicServices_Order", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_ClinicServices_Variant", "\"VisualVariant\" IN ('TEAL','CYAN','NAVY','AQUA','MINT','DARK_TEAL')");
                    table.ForeignKey(
                        name: "FK_ClinicServices_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicServices_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicServices_ClinicId_IsActive_IsPublic_DisplayOrder",
                table: "ClinicServices",
                columns: new[] { "ClinicId", "IsActive", "IsPublic", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicServices_SpecialtyId",
                table: "ClinicServices",
                column: "SpecialtyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicServices");
        }
    }
}
