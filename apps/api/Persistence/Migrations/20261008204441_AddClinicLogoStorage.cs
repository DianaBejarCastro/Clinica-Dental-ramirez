using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicLogoStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoContentType",
                table: "Clinics",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "LogoData",
                table: "Clinics",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoFileName",
                table: "Clinics",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LogoSize",
                table: "Clinics",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LogoUpdatedAt",
                table: "Clinics",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoContentType",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "LogoData",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "LogoFileName",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "LogoSize",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "LogoUpdatedAt",
                table: "Clinics");
        }
    }
}
