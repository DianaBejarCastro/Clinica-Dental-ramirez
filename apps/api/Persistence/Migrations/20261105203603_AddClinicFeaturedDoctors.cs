using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicFeaturedDoctors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicFeaturedDoctors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicFeaturedDoctors", x => x.Id);
                    table.CheckConstraint("CK_ClinicFeaturedDoctors_Order", "\"DisplayOrder\" >= 0");
                    table.ForeignKey(
                        name: "FK_ClinicFeaturedDoctors_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicFeaturedDoctors_DoctorProfiles_DoctorProfileId",
                        column: x => x.DoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicFeaturedDoctors_ClinicId_DoctorProfileId",
                table: "ClinicFeaturedDoctors",
                columns: new[] { "ClinicId", "DoctorProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicFeaturedDoctors_DoctorProfileId",
                table: "ClinicFeaturedDoctors",
                column: "DoctorProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicFeaturedDoctors");
        }
    }
}
