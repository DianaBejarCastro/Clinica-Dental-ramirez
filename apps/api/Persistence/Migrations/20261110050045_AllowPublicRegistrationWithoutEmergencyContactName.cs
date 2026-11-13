using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowPublicRegistrationWithoutEmergencyContactName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientProfiles_RequiredContact",
                table: "PatientProfiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientProfiles_RequiredContact",
                table: "PatientProfiles",
                sql: "length(btrim(\"EmergencyContactPhone\")) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientProfiles_RequiredContact",
                table: "PatientProfiles");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientProfiles_RequiredContact",
                table: "PatientProfiles",
                sql: "length(btrim(\"EmergencyContactName\")) > 0 AND length(btrim(\"EmergencyContactPhone\")) > 0");
        }
    }
}
