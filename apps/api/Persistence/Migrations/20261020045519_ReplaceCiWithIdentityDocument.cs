using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCiWithIdentityDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Ci",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_Ci",
                table: "Users");

            migrationBuilder.AddColumn<string>(name: "DocumentNumber", table: "Users",
                type: "character varying(50)", maxLength: 50, nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentCountryCode",
                table: "Users",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "Users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            // Copy and verify inside EF's migration transaction before removing the source.
            migrationBuilder.Sql("""
                DO $$
                DECLARE expected bigint; migrated bigint;
                BEGIN
                    SELECT count(*) INTO expected FROM "Users" WHERE "Ci" IS NOT NULL AND btrim("Ci") <> '';
                    UPDATE "Users" SET "DocumentType" = 'CI', "DocumentNumber" = "Ci", "DocumentCountryCode" = 'BO'
                    WHERE "Ci" IS NOT NULL AND btrim("Ci") <> '';
                    SELECT count(*) INTO migrated FROM "Users"
                    WHERE "DocumentType" = 'CI' AND "DocumentNumber" = "Ci" AND "DocumentCountryCode" = 'BO';
                    IF expected <> migrated OR EXISTS (
                        SELECT 1 FROM "Users" WHERE "Ci" IS NOT NULL AND "DocumentNumber" IS DISTINCT FROM "Ci"
                    ) THEN RAISE EXCEPTION 'Identity document migration verification failed'; END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(name: "Ci", table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IdentityDocument",
                table: "Users",
                columns: new[] { "DocumentType", "DocumentCountryCode", "DocumentNumber" },
                unique: true,
                filter: "\"DocumentNumber\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_IdentityDocument",
                table: "Users",
                sql: "(\"DocumentType\" IS NULL AND \"DocumentNumber\" IS NULL AND \"DocumentCountryCode\" IS NULL) OR (\"DocumentType\" IS NOT NULL AND \"DocumentType\" IN ('CI', 'PASSPORT') AND \"DocumentNumber\" IS NOT NULL AND length(btrim(\"DocumentNumber\")) > 0 AND \"DocumentNumber\" = upper(btrim(\"DocumentNumber\")) AND \"DocumentCountryCode\" IS NOT NULL AND \"DocumentCountryCode\" ~ '^[A-Z]{2}$' AND (\"DocumentType\" <> 'CI' OR \"DocumentCountryCode\" = 'BO'))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM "Users" WHERE "DocumentType" = 'PASSPORT')
                    THEN RAISE EXCEPTION 'Cannot downgrade identity documents while passports exist'; END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Users_IdentityDocument",
                table: "Users");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Users_IdentityDocument",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DocumentCountryCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Users");

            migrationBuilder.RenameColumn(
                name: "DocumentNumber",
                table: "Users",
                newName: "Ci");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Ci",
                table: "Users",
                column: "Ci",
                unique: true,
                filter: "\"Ci\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Users_Ci",
                table: "Users",
                sql: "\"Ci\" IS NULL OR (length(\"Ci\") > 0 AND \"Ci\" = upper(btrim(\"Ci\")))");
        }
    }
}
