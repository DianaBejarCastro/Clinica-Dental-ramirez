using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOdontogramAndPeriodontalCharting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DentalSurfaces",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalSurfaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DentalTeeth",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    FdiCode = table.Column<int>(type: "integer", nullable: false),
                    DentitionClass = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Quadrant = table.Column<int>(type: "integer", nullable: false),
                    ToothType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalTeeth", x => x.Id);
                    table.CheckConstraint("CK_DentalTeeth_Fdi", "\"FdiCode\" = \"Quadrant\"*10 + \"FdiCode\"%10 AND ((\"DentitionClass\"='PERMANENT' AND \"Quadrant\" BETWEEN 1 AND 4 AND \"FdiCode\"%10 BETWEEN 1 AND 8) OR (\"DentitionClass\"='PRIMARY' AND \"Quadrant\" BETWEEN 5 AND 8 AND \"FdiCode\"%10 BETWEEN 1 AND 5)) AND \"ToothType\" IN ('INCISOR','CANINE','PREMOLAR','MOLAR')");
                });

            migrationBuilder.CreateTable(
                name: "OdontogramExams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicalEncounterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DentitionMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClinicalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreviousExamId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByDoctorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalizedByDoctorProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GeneralNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdontogramExams", x => x.Id);
                    table.CheckConstraint("CK_OdontogramExams_Finalized", "(\"Status\"='DRAFT' AND \"FinalizedAt\" IS NULL AND \"FinalizedByDoctorProfileId\" IS NULL) OR (\"Status\"='FINALIZED' AND \"FinalizedAt\" IS NOT NULL AND \"FinalizedByDoctorProfileId\" IS NOT NULL)");
                    table.CheckConstraint("CK_OdontogramExams_Previous", "\"PreviousExamId\" IS NULL OR \"PreviousExamId\" <> \"Id\"");
                    table.CheckConstraint("CK_OdontogramExams_State", "\"Status\" IN ('DRAFT','FINALIZED') AND \"DentitionMode\" IN ('PERMANENT','PRIMARY','MIXED') AND \"Revision\" >= 0");
                    table.ForeignKey(
                        name: "FK_OdontogramExams_ClinicalEncounters_ClinicalEncounterId",
                        column: x => x.ClinicalEncounterId,
                        principalTable: "ClinicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramExams_DoctorProfiles_CreatedByDoctorProfileId",
                        column: x => x.CreatedByDoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramExams_DoctorProfiles_FinalizedByDoctorProfileId",
                        column: x => x.FinalizedByDoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramExams_OdontogramExams_PreviousExamId",
                        column: x => x.PreviousExamId,
                        principalTable: "OdontogramExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramExams_PatientProfiles_PatientProfileId",
                        column: x => x.PatientProfileId,
                        principalTable: "PatientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OdontogramFindingTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AppliesToSurface = table.Column<bool>(type: "boolean", nullable: false),
                    AppliesToWholeTooth = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdontogramFindingTypes", x => x.Id);
                    table.CheckConstraint("CK_FindingType_Scope", "\"AppliesToSurface\" OR \"AppliesToWholeTooth\"");
                });

            migrationBuilder.CreateTable(
                name: "PeriodontalExams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicalEncounterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DentitionMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ClinicalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PreviousExamId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByDoctorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinalizedByDoctorProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    FinalizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GeneralNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodontalExams", x => x.Id);
                    table.CheckConstraint("CK_PeriodontalExams_Finalized", "(\"Status\"='DRAFT' AND \"FinalizedAt\" IS NULL AND \"FinalizedByDoctorProfileId\" IS NULL) OR (\"Status\"='FINALIZED' AND \"FinalizedAt\" IS NOT NULL AND \"FinalizedByDoctorProfileId\" IS NOT NULL)");
                    table.CheckConstraint("CK_PeriodontalExams_Previous", "\"PreviousExamId\" IS NULL OR \"PreviousExamId\" <> \"Id\"");
                    table.CheckConstraint("CK_PeriodontalExams_State", "\"Status\" IN ('DRAFT','FINALIZED') AND \"DentitionMode\" IN ('PERMANENT','PRIMARY','MIXED') AND \"Revision\" >= 0");
                    table.ForeignKey(
                        name: "FK_PeriodontalExams_ClinicalEncounters_ClinicalEncounterId",
                        column: x => x.ClinicalEncounterId,
                        principalTable: "ClinicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodontalExams_DoctorProfiles_CreatedByDoctorProfileId",
                        column: x => x.CreatedByDoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodontalExams_DoctorProfiles_FinalizedByDoctorProfileId",
                        column: x => x.FinalizedByDoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodontalExams_PatientProfiles_PatientProfileId",
                        column: x => x.PatientProfileId,
                        principalTable: "PatientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodontalExams_PeriodontalExams_PreviousExamId",
                        column: x => x.PreviousExamId,
                        principalTable: "PeriodontalExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodontalSites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SurfaceGroup = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodontalSites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OdontogramFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DentalToothId = table.Column<int>(type: "integer", nullable: false),
                    Observation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OdontogramExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    FindingTypeId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdontogramFindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OdontogramFindings_DentalTeeth_DentalToothId",
                        column: x => x.DentalToothId,
                        principalTable: "DentalTeeth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramFindings_OdontogramExams_OdontogramExamId",
                        column: x => x.OdontogramExamId,
                        principalTable: "OdontogramExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramFindings_OdontogramFindingTypes_FindingTypeId",
                        column: x => x.FindingTypeId,
                        principalTable: "OdontogramFindingTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodontalFurcationAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DentalToothId = table.Column<int>(type: "integer", nullable: false),
                    Observation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodontalExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Location = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Grade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodontalFurcationAssessments", x => x.Id);
                    table.CheckConstraint("CK_PeriodontalFurcation_Range", "\"Grade\" BETWEEN 0 AND 3 AND \"Location\" IN ('BUCCAL','LINGUAL_PALATAL','MESIAL','DISTAL')");
                    table.ForeignKey(
                        name: "FK_PeriodontalFurcationAssessments_DentalTeeth_DentalToothId",
                        column: x => x.DentalToothId,
                        principalTable: "DentalTeeth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodontalFurcationAssessments_PeriodontalExams_Periodonta~",
                        column: x => x.PeriodontalExamId,
                        principalTable: "PeriodontalExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodontalToothAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DentalToothId = table.Column<int>(type: "integer", nullable: false),
                    Observation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodontalExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    MobilityGrade = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodontalToothAssessments", x => x.Id);
                    table.CheckConstraint("CK_PeriodontalMobility_Range", "\"MobilityGrade\" IS NULL OR \"MobilityGrade\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_PeriodontalToothAssessments_DentalTeeth_DentalToothId",
                        column: x => x.DentalToothId,
                        principalTable: "DentalTeeth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodontalToothAssessments_PeriodontalExams_PeriodontalExa~",
                        column: x => x.PeriodontalExamId,
                        principalTable: "PeriodontalExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PeriodontalSiteMeasurements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DentalToothId = table.Column<int>(type: "integer", nullable: false),
                    Observation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodontalExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodontalSiteId = table.Column<int>(type: "integer", nullable: false),
                    ProbingDepthMm = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    GingivalMarginMm = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    BleedingOnProbing = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Plaque = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Suppuration = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodontalSiteMeasurements", x => x.Id);
                    table.CheckConstraint("CK_PeriodontalMeasurement_Range", "(\"ProbingDepthMm\" IS NULL OR \"ProbingDepthMm\" BETWEEN 0 AND 15) AND (\"GingivalMarginMm\" IS NULL OR \"GingivalMarginMm\" BETWEEN -10 AND 20)");
                    table.ForeignKey(
                        name: "FK_PeriodontalSiteMeasurements_DentalTeeth_DentalToothId",
                        column: x => x.DentalToothId,
                        principalTable: "DentalTeeth",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodontalSiteMeasurements_PeriodontalExams_PeriodontalExa~",
                        column: x => x.PeriodontalExamId,
                        principalTable: "PeriodontalExams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PeriodontalSiteMeasurements_PeriodontalSites_PeriodontalSit~",
                        column: x => x.PeriodontalSiteId,
                        principalTable: "PeriodontalSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OdontogramFindingSurfaces",
                columns: table => new
                {
                    OdontogramFindingId = table.Column<Guid>(type: "uuid", nullable: false),
                    DentalSurfaceId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OdontogramFindingSurfaces", x => new { x.OdontogramFindingId, x.DentalSurfaceId });
                    table.ForeignKey(
                        name: "FK_OdontogramFindingSurfaces_DentalSurfaces_DentalSurfaceId",
                        column: x => x.DentalSurfaceId,
                        principalTable: "DentalSurfaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OdontogramFindingSurfaces_OdontogramFindings_OdontogramFind~",
                        column: x => x.OdontogramFindingId,
                        principalTable: "OdontogramFindings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "DentalSurfaces",
                columns: new[] { "Id", "Code", "DisplayOrder", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "VESTIBULAR", 1, true, "Vestibular" },
                    { 2, "OCCLUSAL_INCISAL", 2, true, "Oclusal / incisal" },
                    { 3, "LINGUAL_PALATAL", 3, true, "Lingual / palatina" },
                    { 4, "MESIAL", 4, true, "Mesial" },
                    { 5, "DISTAL", 5, true, "Distal" }
                });

            migrationBuilder.InsertData(
                table: "DentalTeeth",
                columns: new[] { "Id", "DentitionClass", "DisplayOrder", "FdiCode", "IsActive", "Quadrant", "ToothType" },
                values: new object[,]
                {
                    { 11, "PERMANENT", 8, 11, true, 1, "INCISOR" },
                    { 12, "PERMANENT", 7, 12, true, 1, "INCISOR" },
                    { 13, "PERMANENT", 6, 13, true, 1, "CANINE" },
                    { 14, "PERMANENT", 5, 14, true, 1, "PREMOLAR" },
                    { 15, "PERMANENT", 4, 15, true, 1, "PREMOLAR" },
                    { 16, "PERMANENT", 3, 16, true, 1, "MOLAR" },
                    { 17, "PERMANENT", 2, 17, true, 1, "MOLAR" },
                    { 18, "PERMANENT", 1, 18, true, 1, "MOLAR" },
                    { 21, "PERMANENT", 9, 21, true, 2, "INCISOR" },
                    { 22, "PERMANENT", 10, 22, true, 2, "INCISOR" },
                    { 23, "PERMANENT", 11, 23, true, 2, "CANINE" },
                    { 24, "PERMANENT", 12, 24, true, 2, "PREMOLAR" },
                    { 25, "PERMANENT", 13, 25, true, 2, "PREMOLAR" },
                    { 26, "PERMANENT", 14, 26, true, 2, "MOLAR" },
                    { 27, "PERMANENT", 15, 27, true, 2, "MOLAR" },
                    { 28, "PERMANENT", 16, 28, true, 2, "MOLAR" },
                    { 31, "PERMANENT", 25, 31, true, 3, "INCISOR" },
                    { 32, "PERMANENT", 26, 32, true, 3, "INCISOR" },
                    { 33, "PERMANENT", 27, 33, true, 3, "CANINE" },
                    { 34, "PERMANENT", 28, 34, true, 3, "PREMOLAR" },
                    { 35, "PERMANENT", 29, 35, true, 3, "PREMOLAR" },
                    { 36, "PERMANENT", 30, 36, true, 3, "MOLAR" },
                    { 37, "PERMANENT", 31, 37, true, 3, "MOLAR" },
                    { 38, "PERMANENT", 32, 38, true, 3, "MOLAR" },
                    { 41, "PERMANENT", 24, 41, true, 4, "INCISOR" },
                    { 42, "PERMANENT", 23, 42, true, 4, "INCISOR" },
                    { 43, "PERMANENT", 22, 43, true, 4, "CANINE" },
                    { 44, "PERMANENT", 21, 44, true, 4, "PREMOLAR" },
                    { 45, "PERMANENT", 20, 45, true, 4, "PREMOLAR" },
                    { 46, "PERMANENT", 19, 46, true, 4, "MOLAR" },
                    { 47, "PERMANENT", 18, 47, true, 4, "MOLAR" },
                    { 48, "PERMANENT", 17, 48, true, 4, "MOLAR" },
                    { 51, "PRIMARY", 37, 51, true, 5, "INCISOR" },
                    { 52, "PRIMARY", 36, 52, true, 5, "INCISOR" },
                    { 53, "PRIMARY", 35, 53, true, 5, "CANINE" },
                    { 54, "PRIMARY", 34, 54, true, 5, "MOLAR" },
                    { 55, "PRIMARY", 33, 55, true, 5, "MOLAR" },
                    { 61, "PRIMARY", 38, 61, true, 6, "INCISOR" },
                    { 62, "PRIMARY", 39, 62, true, 6, "INCISOR" },
                    { 63, "PRIMARY", 40, 63, true, 6, "CANINE" },
                    { 64, "PRIMARY", 41, 64, true, 6, "MOLAR" },
                    { 65, "PRIMARY", 42, 65, true, 6, "MOLAR" },
                    { 71, "PRIMARY", 48, 71, true, 7, "INCISOR" },
                    { 72, "PRIMARY", 49, 72, true, 7, "INCISOR" },
                    { 73, "PRIMARY", 50, 73, true, 7, "CANINE" },
                    { 74, "PRIMARY", 51, 74, true, 7, "MOLAR" },
                    { 75, "PRIMARY", 52, 75, true, 7, "MOLAR" },
                    { 81, "PRIMARY", 47, 81, true, 8, "INCISOR" },
                    { 82, "PRIMARY", 46, 82, true, 8, "INCISOR" },
                    { 83, "PRIMARY", 45, 83, true, 8, "CANINE" },
                    { 84, "PRIMARY", 44, 84, true, 8, "MOLAR" },
                    { 85, "PRIMARY", 43, 85, true, 8, "MOLAR" }
                });

            migrationBuilder.InsertData(
                table: "OdontogramFindingTypes",
                columns: new[] { "Id", "AppliesToSurface", "AppliesToWholeTooth", "Category", "Code", "DisplayOrder", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, true, false, "CONDITION", "ABFRACTION", 1, true, "Abfraccion" },
                    { 2, true, false, "CONDITION", "CERVICAL_ABRASION", 2, true, "Abrasion cervical" },
                    { 3, true, false, "CONDITION", "CARIES", 3, true, "Caries" },
                    { 4, true, false, "RESTORATION", "AMALGAM_ADAPTED", 4, true, "Amalgama adaptada" },
                    { 5, true, false, "RESTORATION", "AMALGAM_MALADAPTED", 5, true, "Amalgama desadaptada" },
                    { 6, true, true, "RESTORATION", "VENEER_ADAPTED", 6, true, "Carilla adaptada" },
                    { 7, true, true, "RESTORATION", "VENEER_MALADAPTED", 7, true, "Carilla desadaptada" },
                    { 8, false, true, "RESTORATION", "CROWN_ADAPTED", 8, true, "Corona adaptada" },
                    { 9, false, true, "RESTORATION", "CROWN_MALADAPTED", 9, true, "Corona desadaptada" },
                    { 10, true, true, "CONDITION", "HEALTHY", 10, true, "Sano" },
                    { 11, false, true, "CONDITION", "MISSING_TOOTH", 11, true, "Diente ausente" },
                    { 12, false, true, "CONDITION", "IMPACTED_TOOTH", 12, true, "Diente retenido" },
                    { 13, false, true, "CONDITION", "PARTIALLY_ERUPTED", 13, true, "Parcialmente erupcionado" },
                    { 14, false, true, "CONDITION", "WITHOUT_ANTAGONIST", 14, true, "Sin antagonista" },
                    { 15, false, true, "CONDITION", "EXTRACTION_INDICATED", 15, true, "Extraccion indicada" },
                    { 16, true, true, "CONDITION", "FRACTURE", 16, true, "Fractura" },
                    { 17, false, true, "RESTORATION", "IMPLANT_ADAPTED", 17, true, "Implante adaptado" },
                    { 18, false, true, "RESTORATION", "IMPLANT_MALADAPTED", 18, true, "Implante desadaptado" },
                    { 19, false, true, "CONDITION", "PERIAPICAL_LESION", 19, true, "Lesion periapical" },
                    { 20, true, false, "CONDITION", "STAIN", 20, true, "Mancha" },
                    { 21, false, true, "CONDITION", "ENDODONTIC_GOOD", 21, true, "Endodoncia en buen estado" },
                    { 22, false, true, "CONDITION", "ENDODONTIC_INDICATED", 22, true, "Endodoncia indicada" },
                    { 23, false, true, "CONDITION", "ENDODONTIC_POOR", 23, true, "Endodoncia deficiente" },
                    { 24, false, true, "RESTORATION", "INTRARADICULAR_RESTORATION_ADAPTED", 24, true, "Restauracion intrarradicular adaptada" },
                    { 25, false, true, "RESTORATION", "INTRARADICULAR_RESTORATION_MALADAPTED", 25, true, "Restauracion intrarradicular desadaptada" },
                    { 26, false, true, "RESTORATION", "PONTIC", 26, true, "Pontico" },
                    { 27, false, true, "RESTORATION", "PROVISIONAL_ADAPTED", 27, true, "Provisional adaptado" },
                    { 28, false, true, "RESTORATION", "PROVISIONAL_MALADAPTED", 28, true, "Provisional desadaptado" },
                    { 29, false, true, "CONDITION", "ROOT_REMAINS", 29, true, "Restos radiculares" },
                    { 30, true, false, "RESTORATION", "SEALANT_ADAPTED", 30, true, "Sellante adaptado" },
                    { 31, true, false, "RESTORATION", "SEALANT_MALADAPTED", 31, true, "Sellante desadaptado" },
                    { 32, true, false, "CONDITION", "EROSION", 32, true, "Erosion" },
                    { 33, true, true, "CONDITION", "OTHER", 33, true, "Otro hallazgo" }
                });

            migrationBuilder.InsertData(
                table: "PeriodontalSites",
                columns: new[] { "Id", "Code", "DisplayOrder", "IsActive", "Name", "SurfaceGroup" },
                values: new object[,]
                {
                    { 1, "MB", 1, true, "Mesiovestibular", "VESTIBULAR" },
                    { 2, "B", 2, true, "Vestibular", "VESTIBULAR" },
                    { 3, "DB", 3, true, "Distovestibular", "VESTIBULAR" },
                    { 4, "ML", 4, true, "Mesiolingual / mesiopalatino", "LINGUAL_PALATAL" },
                    { 5, "L", 5, true, "Lingual / palatino", "LINGUAL_PALATAL" },
                    { 6, "DL", 6, true, "Distolingual / distopalatino", "LINGUAL_PALATAL" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DentalSurfaces_Code",
                table: "DentalSurfaces",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DentalTeeth_FdiCode",
                table: "DentalTeeth",
                column: "FdiCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramExams_ClinicalEncounterId",
                table: "OdontogramExams",
                column: "ClinicalEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramExams_CreatedByDoctorProfileId",
                table: "OdontogramExams",
                column: "CreatedByDoctorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramExams_FinalizedByDoctorProfileId",
                table: "OdontogramExams",
                column: "FinalizedByDoctorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramExams_PatientProfileId",
                table: "OdontogramExams",
                column: "PatientProfileId",
                unique: true,
                filter: "\"Status\" = 'DRAFT'");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramExams_PatientProfileId_ClinicalDate",
                table: "OdontogramExams",
                columns: new[] { "PatientProfileId", "ClinicalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramExams_PreviousExamId",
                table: "OdontogramExams",
                column: "PreviousExamId",
                unique: true,
                filter: "\"PreviousExamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramFindings_DentalToothId",
                table: "OdontogramFindings",
                column: "DentalToothId");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramFindings_FindingTypeId",
                table: "OdontogramFindings",
                column: "FindingTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramFindings_OdontogramExamId",
                table: "OdontogramFindings",
                column: "OdontogramExamId");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramFindingSurfaces_DentalSurfaceId",
                table: "OdontogramFindingSurfaces",
                column: "DentalSurfaceId");

            migrationBuilder.CreateIndex(
                name: "IX_OdontogramFindingTypes_Code",
                table: "OdontogramFindingTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalExams_ClinicalEncounterId",
                table: "PeriodontalExams",
                column: "ClinicalEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalExams_CreatedByDoctorProfileId",
                table: "PeriodontalExams",
                column: "CreatedByDoctorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalExams_FinalizedByDoctorProfileId",
                table: "PeriodontalExams",
                column: "FinalizedByDoctorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalExams_PatientProfileId",
                table: "PeriodontalExams",
                column: "PatientProfileId",
                unique: true,
                filter: "\"Status\" = 'DRAFT'");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalExams_PatientProfileId_ClinicalDate",
                table: "PeriodontalExams",
                columns: new[] { "PatientProfileId", "ClinicalDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalExams_PreviousExamId",
                table: "PeriodontalExams",
                column: "PreviousExamId",
                unique: true,
                filter: "\"PreviousExamId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalFurcationAssessments_DentalToothId",
                table: "PeriodontalFurcationAssessments",
                column: "DentalToothId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalFurcationAssessments_PeriodontalExamId_DentalToo~",
                table: "PeriodontalFurcationAssessments",
                columns: new[] { "PeriodontalExamId", "DentalToothId", "Location" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalSiteMeasurements_DentalToothId",
                table: "PeriodontalSiteMeasurements",
                column: "DentalToothId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalSiteMeasurements_PeriodontalExamId_DentalToothId~",
                table: "PeriodontalSiteMeasurements",
                columns: new[] { "PeriodontalExamId", "DentalToothId", "PeriodontalSiteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalSiteMeasurements_PeriodontalSiteId",
                table: "PeriodontalSiteMeasurements",
                column: "PeriodontalSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalSites_Code",
                table: "PeriodontalSites",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalToothAssessments_DentalToothId",
                table: "PeriodontalToothAssessments",
                column: "DentalToothId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodontalToothAssessments_PeriodontalExamId_DentalToothId",
                table: "PeriodontalToothAssessments",
                columns: new[] { "PeriodontalExamId", "DentalToothId" },
                unique: true);
            migrationBuilder.Sql("""

CREATE FUNCTION chart_exam_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE previous_patient uuid; previous_status text; previous_date timestamptz;
BEGIN
    IF TG_OP <> 'INSERT' AND OLD."Status"='FINALIZED' THEN
        RAISE EXCEPTION 'Finalized clinical examination is immutable' USING ERRCODE='23514';
    END IF;
    IF TG_OP='DELETE' THEN RETURN OLD; END IF;
    IF TG_OP='INSERT' AND NEW."Status"<>'DRAFT' THEN
        RAISE EXCEPTION 'New clinical examination must start as draft' USING ERRCODE='23514';
    END IF;
    IF TG_OP='UPDATE' AND ROW(NEW."Id",NEW."PatientProfileId",NEW."ClinicalEncounterId",NEW."PreviousExamId",NEW."CreatedByDoctorProfileId",NEW."CreatedAt",NEW."ClinicalDate") IS DISTINCT FROM ROW(OLD."Id",OLD."PatientProfileId",OLD."ClinicalEncounterId",OLD."PreviousExamId",OLD."CreatedByDoctorProfileId",OLD."CreatedAt",OLD."ClinicalDate") THEN
        RAISE EXCEPTION 'Examination identity and lineage cannot be changed' USING ERRCODE='23514';
    END IF;
    IF NEW."ClinicalEncounterId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "ClinicalEncounters" WHERE "Id"=NEW."ClinicalEncounterId" AND "PatientProfileId"=NEW."PatientProfileId" AND "DoctorProfileId"=NEW."CreatedByDoctorProfileId") THEN
        RAISE EXCEPTION 'Encounter does not match examination patient and doctor' USING ERRCODE='23514';
    END IF;
    IF NEW."PreviousExamId" IS NOT NULL THEN
        EXECUTE format('SELECT "PatientProfileId","Status","ClinicalDate" FROM %I WHERE "Id"=$1',TG_TABLE_NAME) INTO previous_patient,previous_status,previous_date USING NEW."PreviousExamId";
        IF previous_patient IS DISTINCT FROM NEW."PatientProfileId" OR previous_status IS DISTINCT FROM 'FINALIZED' OR NEW."ClinicalDate"<previous_date THEN
            RAISE EXCEPTION 'Invalid clinical examination lineage' USING ERRCODE='23514';
        END IF;
    END IF;
    IF NEW."Status"='FINALIZED' AND NEW."FinalizedByDoctorProfileId" IS DISTINCT FROM NEW."CreatedByDoctorProfileId" THEN
        RAISE EXCEPTION 'Only the author can finalize' USING ERRCODE='23514';
    END IF;
    RETURN NEW;
END $$;
CREATE FUNCTION chart_child_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE exam_id uuid; state text; finding_id uuid;
BEGIN
    IF TG_TABLE_NAME='OdontogramFindingSurfaces' THEN
        IF TG_OP='UPDATE' AND ROW(NEW."OdontogramFindingId",NEW."DentalSurfaceId") IS DISTINCT FROM ROW(OLD."OdontogramFindingId",OLD."DentalSurfaceId") THEN RAISE EXCEPTION 'Cannot reparent a finding surface' USING ERRCODE='23514'; END IF;
        IF TG_OP='DELETE' THEN finding_id:=OLD."OdontogramFindingId"; ELSE finding_id:=NEW."OdontogramFindingId"; END IF;
        SELECT "OdontogramExamId" INTO exam_id FROM "OdontogramFindings" WHERE "Id"=finding_id;
    ELSIF TG_TABLE_NAME='OdontogramFindings' THEN
        IF TG_OP='UPDATE' AND NEW."OdontogramExamId" IS DISTINCT FROM OLD."OdontogramExamId" THEN RAISE EXCEPTION 'Cannot reparent a finding' USING ERRCODE='23514'; END IF;
        IF TG_OP='DELETE' THEN exam_id:=OLD."OdontogramExamId"; ELSE exam_id:=NEW."OdontogramExamId"; END IF;
    ELSE
        IF TG_OP='UPDATE' AND NEW."PeriodontalExamId" IS DISTINCT FROM OLD."PeriodontalExamId" THEN RAISE EXCEPTION 'Cannot reparent a measurement' USING ERRCODE='23514'; END IF;
        IF TG_OP='DELETE' THEN exam_id:=OLD."PeriodontalExamId"; ELSE exam_id:=NEW."PeriodontalExamId"; END IF;
    END IF;
    -- Locks serialize child writes with finalization even for direct SQL clients.
    EXECUTE format('SELECT "Status" FROM %I WHERE "Id"=$1 FOR UPDATE',TG_ARGV[0]) INTO state USING exam_id;
    IF state IS DISTINCT FROM 'DRAFT' THEN RAISE EXCEPTION 'Findings of finalized examinations are immutable' USING ERRCODE='23514'; END IF;
    IF TG_OP='DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER "TR_OdontogramExams_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "OdontogramExams" FOR EACH ROW EXECUTE FUNCTION chart_exam_guard();
CREATE TRIGGER "TR_PeriodontalExams_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "PeriodontalExams" FOR EACH ROW EXECUTE FUNCTION chart_exam_guard();
CREATE TRIGGER "TR_OdontogramFindings_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "OdontogramFindings" FOR EACH ROW EXECUTE FUNCTION chart_child_guard('OdontogramExams');
CREATE TRIGGER "TR_OdontogramFindingSurfaces_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "OdontogramFindingSurfaces" FOR EACH ROW EXECUTE FUNCTION chart_child_guard('OdontogramExams');
CREATE TRIGGER "TR_PeriodontalSiteMeasurements_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "PeriodontalSiteMeasurements" FOR EACH ROW EXECUTE FUNCTION chart_child_guard('PeriodontalExams');
CREATE TRIGGER "TR_PeriodontalToothAssessments_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "PeriodontalToothAssessments" FOR EACH ROW EXECUTE FUNCTION chart_child_guard('PeriodontalExams');
CREATE TRIGGER "TR_PeriodontalFurcationAssessments_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "PeriodontalFurcationAssessments" FOR EACH ROW EXECUTE FUNCTION chart_child_guard('PeriodontalExams');
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OdontogramFindingSurfaces");

            migrationBuilder.DropTable(
                name: "PeriodontalFurcationAssessments");

            migrationBuilder.DropTable(
                name: "PeriodontalSiteMeasurements");

            migrationBuilder.DropTable(
                name: "PeriodontalToothAssessments");

            migrationBuilder.DropTable(
                name: "DentalSurfaces");

            migrationBuilder.DropTable(
                name: "OdontogramFindings");

            migrationBuilder.DropTable(
                name: "PeriodontalSites");

            migrationBuilder.DropTable(
                name: "PeriodontalExams");

            migrationBuilder.DropTable(
                name: "DentalTeeth");

            migrationBuilder.DropTable(
                name: "OdontogramExams");

            migrationBuilder.DropTable(
                name: "OdontogramFindingTypes");
            migrationBuilder.Sql("DROP FUNCTION chart_child_guard(); DROP FUNCTION chart_exam_guard();");
        }
    }
}
