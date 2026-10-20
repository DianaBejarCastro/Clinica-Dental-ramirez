using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorPatientIdentityToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_PasswordHash",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_PatientProfiles_Ci",
                table: "PatientProfiles");

            migrationBuilder.DropIndex(
                name: "IX_PatientProfiles_LastName",
                table: "PatientProfiles");

            migrationBuilder.DropIndex(
                name: "IX_PatientProfiles_UserId",
                table: "PatientProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientProfiles_Ci",
                table: "PatientProfiles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PatientProfiles_Names",
                table: "PatientProfiles");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1024)",
                oldMaxLength: 1024);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "character varying(254)",
                maxLength: 254,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(254)",
                oldMaxLength: 254);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BirthDate",
                table: "Users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanLogin",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Ci",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // EF runs this migration atomically. Validate before consolidating or dropping data.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "PatientProfiles" WHERE NULLIF(btrim("Ci"),'') IS NULL
                    OR "BirthDate" IS NULL OR "BirthDate" > (now() AT TIME ZONE 'America/La_Paz')::date
                    OR NULLIF(btrim("Phone"),'') IS NULL OR NULLIF(btrim("EmergencyContactName"),'') IS NULL
                    OR NULLIF(btrim("EmergencyContactPhone"),'') IS NULL OR "BloodType" IS NULL
                    OR "BloodType" NOT IN ('A+','A-','B+','B-','AB+','AB-','O+','O-','UNKNOWN')) THEN
                    RAISE EXCEPTION 'Patient migration requires reviewed clinical data; no values will be invented';
                  END IF;
                  IF EXISTS (SELECT upper(btrim("Ci")) FROM "PatientProfiles" GROUP BY upper(btrim("Ci")) HAVING count(*) > 1)
                    OR EXISTS (SELECT "UserId" FROM "PatientProfiles" WHERE "UserId" IS NOT NULL GROUP BY "UserId" HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Patient CI or UserId conflict';
                  END IF;
                  IF EXISTS (SELECT 1 FROM "PatientProfiles" p JOIN "Users" u ON p."UserId"=u."Id"
                    WHERE btrim(p."FirstName") <> btrim(u."FirstName") OR btrim(p."LastName") <> btrim(u."LastName")
                    OR (u."Phone" IS NOT NULL AND btrim(u."Phone") <> btrim(p."Phone"))
                    OR (p."Email" IS NOT NULL AND lower(btrim(p."Email")) <> u."Email")) THEN
                    RAISE EXCEPTION 'Linked patient personal data conflict; review before migration';
                  END IF;
                  IF EXISTS (SELECT 1 FROM "PatientProfiles") AND NOT EXISTS (SELECT 1 FROM "Roles" WHERE "Code"='PATIENT') THEN
                    RAISE EXCEPTION 'PATIENT role missing';
                  END IF;
                END $$;
                CREATE TEMP TABLE patient_identity_map ON COMMIT DROP AS
                  SELECT "Id" AS patient_id, gen_random_uuid() AS user_id FROM "PatientProfiles" WHERE "UserId" IS NULL;
                INSERT INTO "Users" ("Id","FirstName","LastName","Ci","BirthDate","Gender","Phone","Address",
                  "Email","PasswordHash","CanLogin","IsActive","EmailVerified","CreatedAt","UpdatedAt")
                SELECT m.user_id,btrim(p."FirstName"),btrim(p."LastName"),upper(btrim(p."Ci")),p."BirthDate",p."Gender",btrim(p."Phone"),p."Address",
                  NULL,NULL,false,true,false,p."CreatedAt",p."UpdatedAt"
                FROM "PatientProfiles" p JOIN patient_identity_map m ON m.patient_id=p."Id";
                UPDATE "PatientProfiles" p SET "UserId"=m.user_id FROM patient_identity_map m WHERE m.patient_id=p."Id";
                UPDATE "Users" u SET "Ci"=upper(btrim(p."Ci")),"BirthDate"=p."BirthDate","Gender"=p."Gender",
                  "Phone"=btrim(p."Phone"),"Address"=p."Address"
                FROM "PatientProfiles" p WHERE p."UserId"=u."Id";
                INSERT INTO "UserRoles" ("UserId","RoleId","AssignedAt","AssignedByUserId")
                  SELECT p."UserId",r."Id",now(),NULL FROM "PatientProfiles" p CROSS JOIN "Roles" r WHERE r."Code"='PATIENT'
                  ON CONFLICT ("UserId","RoleId") DO NOTHING;
                """);

            migrationBuilder.DropColumn(
                name: "Address",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "BirthDate",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "Ci",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "FirstName",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "LastName",
                table: "PatientProfiles");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "PatientProfiles");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "PatientProfiles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmergencyContactPhone",
                table: "PatientProfiles",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EmergencyContactName",
                table: "PatientProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BloodType",
                table: "PatientProfiles",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Ci",
                table: "Users",
                column: "Ci",
                unique: true,
                filter: "\"Ci\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastName",
                table: "Users",
                column: "LastName");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Ci",
                table: "Users",
                sql: "\"Ci\" IS NULL OR (length(\"Ci\") > 0 AND \"Ci\" = upper(btrim(\"Ci\")))");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Login",
                table: "Users",
                sql: "(NOT \"CanLogin\" AND \"PasswordHash\" IS NULL) OR (\"CanLogin\" AND \"Email\" IS NOT NULL AND \"PasswordHash\" IS NOT NULL AND length(\"PasswordHash\") > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_PatientProfiles_UserId",
                table: "PatientProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientProfiles_BloodType",
                table: "PatientProfiles",
                sql: "\"BloodType\" IN ('A+','A-','B+','B-','AB+','AB-','O+','O-','UNKNOWN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PatientProfiles_RequiredContact",
                table: "PatientProfiles",
                sql: "length(btrim(\"EmergencyContactName\")) > 0 AND length(btrim(\"EmergencyContactPhone\")) > 0");
        }

        // A rollback cannot represent people without credentials in the previous schema.
        // Restore the verified backup instead of inventing emails/passwords or discarding identities.
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Restore the pre-refactor backup to roll back this data consolidation.");
    }
}
