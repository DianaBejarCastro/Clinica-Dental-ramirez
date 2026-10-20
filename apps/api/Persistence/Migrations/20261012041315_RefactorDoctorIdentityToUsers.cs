using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDoctorIdentityToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse ambiguous data instead of overwriting either identity source.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "DoctorProfiles" d JOIN "Users" u ON u."Id"=d."UserId"
                    WHERE NULLIF(btrim(d."ProfessionalLicense"),'') IS NULL OR NOT u."CanLogin" OR u."Email" IS NULL OR u."PasswordHash" IS NULL
                    OR (u."Ci" IS NOT NULL AND d."Ci" IS NOT NULL AND u."Ci" <> upper(btrim(d."Ci")))
                    OR (u."BirthDate" IS NOT NULL AND d."BirthDate" IS NOT NULL AND u."BirthDate" <> d."BirthDate")
                    OR (u."Phone" IS NOT NULL AND d."Phone" IS NOT NULL AND u."Phone" <> d."Phone")
                    OR (u."Address" IS NOT NULL AND d."Address" IS NOT NULL AND u."Address" <> d."Address")
                    OR (u."Gender" IS NOT NULL AND d."Gender" IS NOT NULL AND u."Gender" <> d."Gender")
                    OR COALESCE(u."BirthDate",d."BirthDate") > (now() AT TIME ZONE 'America/La_Paz')::date) THEN
                    RAISE EXCEPTION 'Doctor identity conflict or invalid credentials/license; manual review required';
                  END IF;
                  IF EXISTS(SELECT 1 FROM "DoctorProfiles") AND NOT EXISTS(SELECT 1 FROM "Roles" WHERE "Code"='DOCTOR' AND "IsActive") THEN
                    RAISE EXCEPTION 'Active DOCTOR role required';
                  END IF;
                END $$;
                UPDATE "Users" u SET "Ci"=COALESCE(u."Ci",NULLIF(upper(btrim(d."Ci")),'')),
                  "BirthDate"=COALESCE(u."BirthDate",d."BirthDate"),"Gender"=COALESCE(u."Gender",d."Gender"),
                  "Phone"=COALESCE(u."Phone",d."Phone"),"Address"=COALESCE(u."Address",d."Address")
                FROM "DoctorProfiles" d WHERE d."UserId"=u."Id";
                INSERT INTO "UserRoles" ("UserId","RoleId","AssignedAt","AssignedByUserId")
                  SELECT d."UserId",r."Id",now(),NULL FROM "DoctorProfiles" d CROSS JOIN "Roles" r WHERE r."Code"='DOCTOR'
                  ON CONFLICT ("UserId","RoleId") DO NOTHING;
                """);
            migrationBuilder.DropColumn(
                name: "Address",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "Ci",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "DoctorProfiles");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "DoctorProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "ProfessionalLicense",
                table: "DoctorProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProfessionalLicense",
                table: "DoctorProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "DoctorProfiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "DoctorProfiles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ci",
                table: "DoctorProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "DoctorProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "DoctorProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE "DoctorProfiles" d SET "Ci"=u."Ci","BirthDate"=u."BirthDate","Gender"=u."Gender","Phone"=u."Phone","Address"=u."Address"
                FROM "Users" u WHERE u."Id"=d."UserId";
                """);
        }
    }
}
