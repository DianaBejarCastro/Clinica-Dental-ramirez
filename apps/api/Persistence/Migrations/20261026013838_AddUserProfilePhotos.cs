using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfilePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "DoctorProfiles" WHERE "PhotoUrl" IS NOT NULL AND btrim("PhotoUrl") <> '') THEN
                    RAISE EXCEPTION 'Existing doctor photo URLs must be migrated before applying AddUserProfilePhotos';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "PhotoUrl",
                table: "DoctorProfiles");

            migrationBuilder.CreateTable(
                name: "UserProfilePhotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfilePhotos", x => x.Id);
                    table.CheckConstraint("CK_UserProfilePhotos_Dimensions", "\"Width\" = 1080 AND \"Height\" = 1350");
                    table.CheckConstraint("CK_UserProfilePhotos_Size", "\"FileSizeBytes\" > 0 AND \"FileSizeBytes\" <= 5242880 AND octet_length(\"ImageData\") = \"FileSizeBytes\"");
                    table.CheckConstraint("CK_UserProfilePhotos_Type", "\"MimeType\" IN ('image/png','image/jpeg','image/webp')");
                    table.ForeignKey(
                        name: "FK_UserProfilePhotos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserProfilePhotos_UserId",
                table: "UserProfilePhotos",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserProfilePhotos");

            migrationBuilder.AddColumn<string>(
                name: "PhotoUrl",
                table: "DoctorProfiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }
    }
}
