using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExciseTaxAudit.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundClaimsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RefundClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EngagementId = table.Column<int>(type: "int", nullable: false),
                    AuditCaseId = table.Column<int>(type: "int", nullable: true),
                    ClaimNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RefundType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TaxType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxFormType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClaimedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TaxPeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaxPeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TaxYear = table.Column<int>(type: "int", nullable: false),
                    TaxQuarter = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransactionCount = table.Column<int>(type: "int", nullable: false),
                    TransactionIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Justification = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IRSCitations = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupportingDocuments = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EIN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameOfClaimant = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimantAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmittedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IRSResponseNotes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpectedPaymentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InternalNotes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: false),
                    AIRecommendation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AIReasoning = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Form8849Path = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupportingEvidencePath = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundClaims_AuditCases_AuditCaseId",
                        column: x => x.AuditCaseId,
                        principalTable: "AuditCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_RefundClaims_Engagements_EngagementId",
                        column: x => x.EngagementId,
                        principalTable: "Engagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefundClaims_AuditCaseId",
                table: "RefundClaims",
                column: "AuditCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundClaims_ClaimNumber",
                table: "RefundClaims",
                column: "ClaimNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefundClaims_CreatedDate",
                table: "RefundClaims",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_RefundClaims_EngagementId",
                table: "RefundClaims",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundClaims_Status",
                table: "RefundClaims",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RefundClaims_SubmittedDate",
                table: "RefundClaims",
                column: "SubmittedDate");

            migrationBuilder.CreateIndex(
                name: "IX_RefundClaims_TaxYear",
                table: "RefundClaims",
                column: "TaxYear");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefundClaims");
        }
    }
}
