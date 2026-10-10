using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicSocialNetworks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicSocialNetworks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LogoData = table.Column<byte[]>(type: "bytea", nullable: true),
                    LogoContentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LogoFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    LogoSize = table.Column<long>(type: "bigint", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSocialNetworks", x => x.Id);
                    table.CheckConstraint("CK_ClinicSocialNetworks_Name", "length(btrim(\"Name\")) > 0 AND \"Name\" = btrim(\"Name\")");
                    table.CheckConstraint("CK_ClinicSocialNetworks_Order", "\"DisplayOrder\" >= 0");
                    table.CheckConstraint("CK_ClinicSocialNetworks_Url", "\"Url\" ~* '^https?://[^[:space:]]+$'");
                    table.ForeignKey(
                        name: "FK_ClinicSocialNetworks_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSocialNetworks_ClinicId",
                table: "ClinicSocialNetworks",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSocialNetworks_ClinicId_DisplayOrder",
                table: "ClinicSocialNetworks",
                columns: new[] { "ClinicId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSocialNetworks_ClinicId_IsActive",
                table: "ClinicSocialNetworks",
                columns: new[] { "ClinicId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSocialNetworks_ClinicId_Url",
                table: "ClinicSocialNetworks",
                columns: new[] { "ClinicId", "Url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicSocialNetworks");
        }
    }
}
