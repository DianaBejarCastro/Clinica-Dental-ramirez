using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientExternalLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Login",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Names",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientProfiles_BloodType",
                table: "PatientProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientProfiles_RequiredContact",
                table: "PatientProfiles");

            migrationBuilder.AddColumn<bool>(
                name: "PatientOnly",
                table: "RefreshTokens",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserExternalLogins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProviderSubject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProviderEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExternalLogins", x => x.Id);
                    table.CheckConstraint("CK_UserExternalLogins_Provider", "\"Provider\" = 'GOOGLE'");
                    table.CheckConstraint("CK_UserExternalLogins_Subject", "length(btrim(\"ProviderSubject\")) > 0");
                    table.ForeignKey(
                        name: "FK_UserExternalLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Login",
                table: "Users",
                sql: "(NOT \"CanLogin\" AND \"PasswordHash\" IS NULL) OR (\"CanLogin\" AND \"Email\" IS NOT NULL AND (\"PasswordHash\" IS NULL OR length(\"PasswordHash\") > 0))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Names",
                table: "Users",
                sql: "length(btrim(\"FirstName\")) > 0 AND length(\"LastName\") >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientProfiles_BloodType",
                table: "PatientProfiles",
                sql: "\"BloodType\" IN ('','A+','A-','B+','B-','AB+','AB-','O+','O-','UNKNOWN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientProfiles_RequiredContact",
                table: "PatientProfiles",
                sql: "length(btrim(\"EmergencyContactPhone\")) >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalLogins_Provider_ProviderSubject",
                table: "UserExternalLogins",
                columns: new[] { "Provider", "ProviderSubject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserExternalLogins_UserId_Provider",
                table: "UserExternalLogins",
                columns: new[] { "UserId", "Provider" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserExternalLogins");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Login",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Names",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientProfiles_BloodType",
                table: "PatientProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientProfiles_RequiredContact",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "PatientOnly",
                table: "RefreshTokens");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Login",
                table: "Users",
                sql: "(NOT \"CanLogin\" AND \"PasswordHash\" IS NULL) OR (\"CanLogin\" AND \"Email\" IS NOT NULL AND \"PasswordHash\" IS NOT NULL AND length(\"PasswordHash\") > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Names",
                table: "Users",
                sql: "length(btrim(\"FirstName\")) > 0 AND length(btrim(\"LastName\")) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientProfiles_BloodType",
                table: "PatientProfiles",
                sql: "\"BloodType\" IN ('A+','A-','B+','B-','AB+','AB-','O+','O-','UNKNOWN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientProfiles_RequiredContact",
                table: "PatientProfiles",
                sql: "length(btrim(\"EmergencyContactPhone\")) > 0");
        }
    }
}
