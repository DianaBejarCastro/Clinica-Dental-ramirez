using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDentalAssistantProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DentalAssistantProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalAssistantProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalAssistantProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DentalAssistantProfiles_IsActive",
                table: "DentalAssistantProfiles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DentalAssistantProfiles_UserId",
                table: "DentalAssistantProfiles",
                column: "UserId",
                unique: true);
            // Existing role holders receive only the missing domain profile; identity and credentials remain untouched.
            migrationBuilder.Sql("""
                WITH inserted AS (
                  INSERT INTO "DentalAssistantProfiles" ("Id","UserId","IsActive","CreatedAt","UpdatedAt")
                  SELECT gen_random_uuid(),u."Id",u."IsActive",u."CreatedAt",u."UpdatedAt"
                  FROM "Users" u JOIN "UserRoles" ur ON ur."UserId"=u."Id" JOIN "Roles" r ON r."Id"=ur."RoleId"
                  WHERE r."Code"='DENTAL_ASSISTANT'
                  ON CONFLICT ("UserId") DO NOTHING RETURNING "Id"
                )
                INSERT INTO "AuditLogs" ("Id","Action","Entity","EntityId","CreatedAt","MetadataJson")
                  SELECT gen_random_uuid(),'dental_assistant_created','DentalAssistantProfile',"Id",now(),'{"migration":true}'::jsonb FROM inserted;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DentalAssistantProfiles");
        }
    }
}
