using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicMultimediaGallery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicMediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Caption = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AltText = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LegacyImageId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicMediaItems", x => x.Id);
                    table.CheckConstraint("CK_ClinicMedia_Order", "\"SortOrder\" >= 0");
                    table.CheckConstraint("CK_ClinicMedia_Type", "\"MediaType\" IN ('IMAGE','VIDEO_UPLOAD','TIKTOK')");
                    table.ForeignKey(
                        name: "FK_ClinicMediaItems_ClinicSiteImages_LegacyImageId",
                        column: x => x.LegacyImageId,
                        principalTable: "ClinicSiteImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicMediaItems_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicMediaAssets",
                columns: table => new
                {
                    MediaItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: true),
                    ThumbnailData = table.Column<byte[]>(type: "bytea", nullable: true),
                    StorageKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MimeType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: true),
                    Height = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicMediaAssets", x => x.MediaItemId);
                    table.CheckConstraint("CK_MediaAsset_Storage", "(\"ImageData\" IS NOT NULL AND \"StorageKey\" IS NULL AND \"FileSizeBytes\" BETWEEN 1 AND 10485760) OR (\"ImageData\" IS NULL AND \"StorageKey\" IS NOT NULL AND \"FileSizeBytes\" BETWEEN 1 AND 52428800)");
                    table.ForeignKey(
                        name: "FK_ClinicMediaAssets_ClinicMediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "ClinicMediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicMediaExternalSources",
                columns: table => new
                {
                    MediaItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicMediaExternalSources", x => x.MediaItemId);
                    table.ForeignKey(
                        name: "FK_ClinicMediaExternalSources_ClinicMediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "ClinicMediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMediaItems_ClinicId_IsActive_SortOrder",
                table: "ClinicMediaItems",
                columns: new[] { "ClinicId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicMediaItems_LegacyImageId",
                table: "ClinicMediaItems",
                column: "LegacyImageId",
                unique: true);
            // Preserve existing cover bytes and IDs via a reference; CARE remains independent.
            migrationBuilder.Sql("""
                INSERT INTO "ClinicMediaItems" ("Id","ClinicId","MediaType","Title","AltText","SortOrder","IsActive","CreatedAt","UpdatedAt","LegacyImageId")
                SELECT "Id","ClinicId",'IMAGE','Instalaciones de la clínica',COALESCE(NULLIF("AltText",''),'Instalaciones de la clínica'),0,"IsActive","CreatedAt","UpdatedAt","Id"
                FROM "ClinicSiteImages" WHERE "ImageType" = 'HERO';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicMediaAssets");

            migrationBuilder.DropTable(
                name: "ClinicMediaExternalSources");

            migrationBuilder.DropTable(
                name: "ClinicMediaItems");
        }
    }
}
