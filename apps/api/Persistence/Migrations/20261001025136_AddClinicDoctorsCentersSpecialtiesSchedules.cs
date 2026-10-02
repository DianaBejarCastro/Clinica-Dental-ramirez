using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicDoctorsCentersSpecialtiesSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clinics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Whatsapp = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Website = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TaxId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clinics", x => x.Id);
                    table.CheckConstraint("CK_Clinics_Name", "length(btrim(\"Name\")) > 0");
                    table.CheckConstraint("CK_Clinics_Singleton", "\"Id\" = '00000000-0000-0000-0000-000000000001'::uuid");
                });

            migrationBuilder.CreateTable(
                name: "DoctorProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfessionalLicense = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Ci = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Gender = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PhotoUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Bio = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorProfiles", x => x.Id);
                    table.CheckConstraint("CK_DoctorProfiles_License", "\"ProfessionalLicense\" IS NULL OR (length(btrim(\"ProfessionalLicense\")) > 0 AND \"ProfessionalLicense\" = upper(btrim(\"ProfessionalLicense\")))");
                    table.ForeignKey(
                        name: "FK_DoctorProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Specialties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Specialties", x => x.Id);
                    table.CheckConstraint("CK_Specialties_Code", "\"Code\" ~ '^[A-Z][A-Z0-9_]*$'");
                    table.CheckConstraint("CK_Specialties_Name", "length(btrim(\"Name\")) > 0");
                });

            migrationBuilder.CreateTable(
                name: "Centers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Whatsapp = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    GoogleMapsUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Centers", x => x.Id);
                    table.CheckConstraint("CK_Centers_Code", "\"Code\" ~ '^[A-Z][A-Z0-9_]*$'");
                    table.CheckConstraint("CK_Centers_Latitude", "\"Latitude\" IS NULL OR \"Latitude\" BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_Centers_Longitude", "\"Longitude\" IS NULL OR \"Longitude\" BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_Centers_RequiredText", "length(btrim(\"Name\")) > 0 AND length(btrim(\"Address\")) > 0 AND length(btrim(\"Country\")) > 0");
                    table.ForeignKey(
                        name: "FK_Centers_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSpecialties",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecialtyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSpecialties", x => new { x.DoctorId, x.SpecialtyId });
                    table.ForeignKey(
                        name: "FK_DoctorSpecialties_DoctorProfiles_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSpecialties_Specialties_SpecialtyId",
                        column: x => x.SpecialtyId,
                        principalTable: "Specialties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorCenters",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorCenters", x => new { x.DoctorId, x.CenterId });
                    table.ForeignKey(
                        name: "FK_DoctorCenters_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorCenters_DoctorProfiles_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CenterId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    HasBreak = table.Column<bool>(type: "boolean", nullable: false),
                    BreakStart = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    BreakEnd = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSchedules", x => x.Id);
                    table.CheckConstraint("CK_DoctorSchedules_Break", "(NOT \"HasBreak\" AND \"BreakStart\" IS NULL AND \"BreakEnd\" IS NULL) OR (\"HasBreak\" AND \"BreakStart\" IS NOT NULL AND \"BreakEnd\" IS NOT NULL AND \"StartTime\" < \"BreakStart\" AND \"BreakStart\" < \"BreakEnd\" AND \"BreakEnd\" < \"EndTime\")");
                    table.CheckConstraint("CK_DoctorSchedules_Day", "\"DayOfWeek\" BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_DoctorSchedules_Times", "\"StartTime\" < \"EndTime\"");
                    table.ForeignKey(
                        name: "FK_DoctorSchedules_Centers_CenterId",
                        column: x => x.CenterId,
                        principalTable: "Centers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSchedules_DoctorCenters_DoctorId_CenterId",
                        columns: x => new { x.DoctorId, x.CenterId },
                        principalTable: "DoctorCenters",
                        principalColumns: new[] { "DoctorId", "CenterId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSchedules_DoctorProfiles_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Centers_ActiveMain",
                table: "Centers",
                column: "ClinicId",
                unique: true,
                filter: "\"IsMain\" AND \"IsActive\"");

            migrationBuilder.CreateIndex(
                name: "IX_Centers_ClinicId_IsActive",
                table: "Centers",
                columns: new[] { "ClinicId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Centers_Code",
                table: "Centers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorCenters_CenterId",
                table: "DoctorCenters",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorProfiles_IsActive",
                table: "DoctorProfiles",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorProfiles_ProfessionalLicense",
                table: "DoctorProfiles",
                column: "ProfessionalLicense",
                unique: true,
                filter: "\"ProfessionalLicense\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorProfiles_UserId",
                table: "DoctorProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSchedules_CenterId",
                table: "DoctorSchedules",
                column: "CenterId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSchedules_DoctorId_CenterId",
                table: "DoctorSchedules",
                columns: new[] { "DoctorId", "CenterId" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSchedules_DoctorId_DayOfWeek_IsActive",
                table: "DoctorSchedules",
                columns: new[] { "DoctorId", "DayOfWeek", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSpecialties_SpecialtyId",
                table: "DoctorSpecialties",
                column: "SpecialtyId");

            migrationBuilder.CreateIndex(
                name: "IX_Specialties_Code",
                table: "Specialties",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Specialties_Name",
                table: "Specialties",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DoctorSchedules");

            migrationBuilder.DropTable(
                name: "DoctorSpecialties");

            migrationBuilder.DropTable(
                name: "DoctorCenters");

            migrationBuilder.DropTable(
                name: "Specialties");

            migrationBuilder.DropTable(
                name: "Centers");

            migrationBuilder.DropTable(
                name: "DoctorProfiles");

            migrationBuilder.DropTable(
                name: "Clinics");
        }
    }
}
