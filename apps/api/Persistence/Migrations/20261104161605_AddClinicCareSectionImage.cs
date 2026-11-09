using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicCareSectionImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ClinicSiteImages_Hero",
                table: "ClinicSiteImages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClinicSiteImages_Purpose",
                table: "ClinicSiteImages",
                sql: "(\"ImageType\" = 'HERO' AND \"Width\" = 1916 AND \"Height\" = 821) OR (\"ImageType\" = 'CARE' AND \"Width\" = 1200 AND \"Height\" = 1350)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ClinicSiteImages_Purpose",
                table: "ClinicSiteImages");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ClinicSiteImages_Hero",
                table: "ClinicSiteImages",
                sql: "\"ImageType\" = 'HERO' AND \"Width\" = 1916 AND \"Height\" = 821");
        }
    }
}
