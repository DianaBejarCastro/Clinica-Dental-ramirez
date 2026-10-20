using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClinicaRamirez.Api.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicalCareTreatmentAndMedicalHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientMedicalHistoryReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateReviewId = table.Column<Guid>(type: "uuid", nullable: true),
                    NoKnownAllergies = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Notes = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMedicalHistoryReviews", x => x.Id);
                    table.CheckConstraint("CK_PatientMedicalHistoryReviews_Domain", "\"StateReviewId\" IS NULL OR \"StateReviewId\" <> \"Id\"");
                    table.ForeignKey(
                        name: "FK_PatientMedicalHistoryReviews_PatientMedicalHistoryReviews_S~",
                        column: x => x.StateReviewId,
                        principalTable: "PatientMedicalHistoryReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientMedicalHistoryReviews_PatientProfiles_PatientProfile~",
                        column: x => x.PatientProfileId,
                        principalTable: "PatientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientMedicalHistoryReviews_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryDoctorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentEpisodes", x => x.Id);
                    table.CheckConstraint("CK_TreatmentEpisodes_Domain", "\"Status\" IN ('OPEN','COMPLETED','CANCELLED') AND length(btrim(\"Title\"))>0");
                    table.ForeignKey(
                        name: "FK_TreatmentEpisodes_DoctorProfiles_PrimaryDoctorProfileId",
                        column: x => x.PrimaryDoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentEpisodes_PatientProfiles_PatientProfileId",
                        column: x => x.PatientProfileId,
                        principalTable: "PatientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientAllergyEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalHistoryReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reaction = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Severity = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    IsCurrentlyActive = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientAllergyEntries", x => x.Id);
                    table.CheckConstraint("CK_PatientAllergyEntries_Domain", "length(btrim(\"Name\"))>0");
                    table.ForeignKey(
                        name: "FK_PatientAllergyEntries_PatientMedicalHistoryReviews_MedicalH~",
                        column: x => x.MedicalHistoryReviewId,
                        principalTable: "PatientMedicalHistoryReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientConditionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalHistoryReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConditionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Details = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    IsCurrentlyActive = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientConditionEntries", x => x.Id);
                    table.CheckConstraint("CK_PatientConditionEntries_Domain", "length(btrim(\"ConditionName\"))>0");
                    table.ForeignKey(
                        name: "FK_PatientConditionEntries_PatientMedicalHistoryReviews_Medica~",
                        column: x => x.MedicalHistoryReviewId,
                        principalTable: "PatientMedicalHistoryReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientMedicationEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalHistoryReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicationName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dose = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Frequency = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Reason = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    IsCurrentlyActive = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientMedicationEntries", x => x.Id);
                    table.CheckConstraint("CK_PatientMedicationEntries_Domain", "length(btrim(\"MedicationName\"))>0");
                    table.ForeignKey(
                        name: "FK_PatientMedicationEntries_PatientMedicalHistoryReviews_Medic~",
                        column: x => x.MedicalHistoryReviewId,
                        principalTable: "PatientMedicalHistoryReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClinicalEncounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentEpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DoctorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    MedicalHistoryReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEncounters", x => x.Id);
                    table.CheckConstraint("CK_ClinicalEncounters_Domain", "\"Status\" IN ('OPEN','COMPLETED') AND (\"EndedAt\" IS NULL OR \"EndedAt\">=\"StartedAt\")");
                    table.ForeignKey(
                        name: "FK_ClinicalEncounters_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEncounters_DoctorProfiles_DoctorProfileId",
                        column: x => x.DoctorProfileId,
                        principalTable: "DoctorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEncounters_PatientMedicalHistoryReviews_MedicalHist~",
                        column: x => x.MedicalHistoryReviewId,
                        principalTable: "PatientMedicalHistoryReviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEncounters_PatientProfiles_PatientProfileId",
                        column: x => x.PatientProfileId,
                        principalTable: "PatientProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicalEncounters_TreatmentEpisodes_TreatmentEpisodeId",
                        column: x => x.TreatmentEpisodeId,
                        principalTable: "TreatmentEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentAnamneses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentEpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChiefComplaint = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    PainSince = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    IsPainConstant = table.Column<bool>(type: "boolean", nullable: true),
                    ColdSensitivity = table.Column<bool>(type: "boolean", nullable: true),
                    HeatSensitivity = table.Column<bool>(type: "boolean", nullable: true),
                    PainWhenChewing = table.Column<bool>(type: "boolean", nullable: true),
                    WakesFromPain = table.Column<bool>(type: "boolean", nullable: true),
                    PainMedicationTaken = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    PainIntensity = table.Column<int>(type: "integer", nullable: true),
                    InitialObservations = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentAnamneses", x => x.Id);
                    table.CheckConstraint("CK_TreatmentAnamneses_Domain", "length(btrim(\"ChiefComplaint\"))>0 AND length(btrim(\"PainSince\"))>0 AND (\"PainIntensity\" IS NULL OR \"PainIntensity\" BETWEEN 0 AND 10)");
                    table.ForeignKey(
                        name: "FK_TreatmentAnamneses_TreatmentEpisodes_TreatmentEpisodeId",
                        column: x => x.TreatmentEpisodeId,
                        principalTable: "TreatmentEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentAnamneses_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentEpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentPlans", x => x.Id);
                    table.CheckConstraint("CK_TreatmentPlans_Domain", "\"Status\" IN ('DRAFT','PROPOSED','ACCEPTED','IN_PROGRESS','COMPLETED','CANCELLED') AND length(btrim(\"Title\"))>0");
                    table.ForeignKey(
                        name: "FK_TreatmentPlans_TreatmentEpisodes_TreatmentEpisodeId",
                        column: x => x.TreatmentEpisodeId,
                        principalTable: "TreatmentEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoapNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicalEncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subjective = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Objective = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Assessment = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Plan = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SignedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoapNotes", x => x.Id);
                    table.CheckConstraint("CK_SoapNotes_Domain", "(\"Status\"='DRAFT' AND \"SignedAt\" IS NULL AND \"SignedByUserId\" IS NULL) OR (\"Status\"='SIGNED' AND \"SignedAt\" IS NOT NULL AND \"SignedByUserId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_SoapNotes_ClinicalEncounters_ClinicalEncounterId",
                        column: x => x.ClinicalEncounterId,
                        principalTable: "ClinicalEncounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoapNotes_Users_SignedByUserId",
                        column: x => x.SignedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentFinancialAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgreedTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentFinancialAccounts", x => x.Id);
                    table.CheckConstraint("CK_TreatmentFinancialAccounts_Domain", "\"AgreedTotal\">=0");
                    table.ForeignKey(
                        name: "FK_TreatmentFinancialAccounts_TreatmentPlans_TreatmentPlanId",
                        column: x => x.TreatmentPlanId,
                        principalTable: "TreatmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentPlanItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TreatmentPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    ToothNumber = table.Column<int>(type: "integer", nullable: true),
                    ToothSurface = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentPlanItems", x => x.Id);
                    table.CheckConstraint("CK_TreatmentPlanItems_Domain", "\"UnitPrice\">=0 AND \"Quantity\">0 AND length(btrim(\"ProcedureName\"))>0 AND \"Status\" IN ('PROPOSED','ACCEPTED','IN_PROGRESS','COMPLETED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_TreatmentPlanItems_TreatmentPlans_TreatmentPlanId",
                        column: x => x.TreatmentPlanId,
                        principalTable: "TreatmentPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoapAddenda",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Content = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoapAddenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoapAddenda_SoapNotes_OriginalEntryId",
                        column: x => x.OriginalEntryId,
                        principalTable: "SoapNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SoapAddenda_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TreatmentPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancialAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Reference = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    RecordedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TreatmentPayments", x => x.Id);
                    table.CheckConstraint("CK_TreatmentPayments_Domain", "\"Amount\">0");
                    table.ForeignKey(
                        name: "FK_TreatmentPayments_TreatmentFinancialAccounts_FinancialAccou~",
                        column: x => x.FinancialAccountId,
                        principalTable: "TreatmentFinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TreatmentPayments_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEncounters_AppointmentId",
                table: "ClinicalEncounters",
                column: "AppointmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEncounters_DoctorProfileId",
                table: "ClinicalEncounters",
                column: "DoctorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEncounters_MedicalHistoryReviewId",
                table: "ClinicalEncounters",
                column: "MedicalHistoryReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEncounters_PatientProfileId",
                table: "ClinicalEncounters",
                column: "PatientProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEncounters_TreatmentEpisodeId",
                table: "ClinicalEncounters",
                column: "TreatmentEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientAllergyEntries_MedicalHistoryReviewId",
                table: "PatientAllergyEntries",
                column: "MedicalHistoryReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientConditionEntries_MedicalHistoryReviewId",
                table: "PatientConditionEntries",
                column: "MedicalHistoryReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicalHistoryReviews_PatientProfileId_ReviewedAt",
                table: "PatientMedicalHistoryReviews",
                columns: new[] { "PatientProfileId", "ReviewedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicalHistoryReviews_ReviewedByUserId",
                table: "PatientMedicalHistoryReviews",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicalHistoryReviews_StateReviewId",
                table: "PatientMedicalHistoryReviews",
                column: "StateReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientMedicationEntries_MedicalHistoryReviewId",
                table: "PatientMedicationEntries",
                column: "MedicalHistoryReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_SoapAddenda_CreatedByUserId",
                table: "SoapAddenda",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SoapAddenda_OriginalEntryId",
                table: "SoapAddenda",
                column: "OriginalEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SoapNotes_ClinicalEncounterId",
                table: "SoapNotes",
                column: "ClinicalEncounterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SoapNotes_SignedByUserId",
                table: "SoapNotes",
                column: "SignedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentAnamneses_RecordedByUserId",
                table: "TreatmentAnamneses",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentAnamneses_TreatmentEpisodeId",
                table: "TreatmentAnamneses",
                column: "TreatmentEpisodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentEpisodes_PatientProfileId",
                table: "TreatmentEpisodes",
                column: "PatientProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentEpisodes_PrimaryDoctorProfileId",
                table: "TreatmentEpisodes",
                column: "PrimaryDoctorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentFinancialAccounts_TreatmentPlanId",
                table: "TreatmentFinancialAccounts",
                column: "TreatmentPlanId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPayments_FinancialAccountId_IdempotencyKey",
                table: "TreatmentPayments",
                columns: new[] { "FinancialAccountId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPayments_RecordedByUserId",
                table: "TreatmentPayments",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlanItems_TreatmentPlanId",
                table: "TreatmentPlanItems",
                column: "TreatmentPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TreatmentPlans_TreatmentEpisodeId",
                table: "TreatmentPlans",
                column: "TreatmentEpisodeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientAllergyEntries");

            migrationBuilder.DropTable(
                name: "PatientConditionEntries");

            migrationBuilder.DropTable(
                name: "PatientMedicationEntries");

            migrationBuilder.DropTable(
                name: "SoapAddenda");

            migrationBuilder.DropTable(
                name: "TreatmentAnamneses");

            migrationBuilder.DropTable(
                name: "TreatmentPayments");

            migrationBuilder.DropTable(
                name: "TreatmentPlanItems");

            migrationBuilder.DropTable(
                name: "SoapNotes");

            migrationBuilder.DropTable(
                name: "TreatmentFinancialAccounts");

            migrationBuilder.DropTable(
                name: "ClinicalEncounters");

            migrationBuilder.DropTable(
                name: "TreatmentPlans");

            migrationBuilder.DropTable(
                name: "PatientMedicalHistoryReviews");

            migrationBuilder.DropTable(
                name: "TreatmentEpisodes");
        }
    }
}
