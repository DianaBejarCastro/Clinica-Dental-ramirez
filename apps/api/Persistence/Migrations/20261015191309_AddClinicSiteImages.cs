using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicSiteImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicSiteImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    FocalPointX = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: true),
                    FocalPointY = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: true),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSiteImages", x => x.Id);
                    table.CheckConstraint("CK_ClinicSiteImages_Content", "\"ContentType\" IN ('image/png','image/jpeg','image/webp') AND \"Size\" > 0 AND \"Size\" <= 10485760 AND octet_length(\"ImageData\") = \"Size\"");
                    table.CheckConstraint("CK_ClinicSiteImages_FocalPoint", "(\"FocalPointX\" IS NULL OR \"FocalPointX\" BETWEEN 0 AND 1) AND (\"FocalPointY\" IS NULL OR \"FocalPointY\" BETWEEN 0 AND 1)");
                    table.CheckConstraint("CK_ClinicSiteImages_Hero", "\"ImageType\" = 'HERO' AND \"Width\" = 1916 AND \"Height\" = 821");
                    table.ForeignKey(
                        name: "FK_ClinicSiteImages_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicSiteImages_ClinicId_ImageType",
                table: "ClinicSiteImages",
                columns: new[] { "ClinicId", "ImageType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicSiteImages");
        }
    }
}
