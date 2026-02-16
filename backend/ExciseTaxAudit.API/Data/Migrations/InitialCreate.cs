using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExciseTaxAudit.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransactionRecords",
                columns: table => new
                {
                    RecordID = table.Column<long>(type: "bigint", nullable: false),
                    Branch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Department = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Entity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BillingCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CardNumberMask = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FuelPlatform = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FuelType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Discrepancies = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeID = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MerchantName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MerchantCity = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MerchantState = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PADDRegion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssetNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AssetDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Odometer = table.Column<long>(type: "bigint", nullable: true),
                    ProductDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProductType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PricePerUnit = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    NetCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GrossCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UOM = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PostedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CardTypeFlag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CardholderName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TripDispatchQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    ReportingLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MCC = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MerchantCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MerchantAddress1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MerchantAddress2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MerchantPostalCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChainCode = table.Column<int>(type: "int", nullable: true),
                    CrossBorderTransactionAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmountDue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TransactionFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FuelTransactionID = table.Column<long>(type: "bigint", nullable: true),
                    FuelTransactionDetailID = table.Column<long>(type: "bigint", nullable: true),
                    BranchDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalBranch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OriginalBranchDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedComments = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentProcessedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaymentProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TransactionTimezone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    K_EquipmentDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    K_SafeHarborPercentage = table.Column<decimal>(type: "decimal(5,4)", nullable: true),
                    IngestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceFileUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Label_OverUnder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Label_AdjustmentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AnomalyScore = table.Column<float>(type: "real", nullable: true),
                    AnomalyReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsReviewed = table.Column<bool>(type: "bit", nullable: false),
                    AuditorNotes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionRecords", x => x.RecordID);
                });

            migrationBuilder.CreateTable(
                name: "UploadJobs",
                columns: table => new
                {
                    JobId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalRecords = table.Column<int>(type: "int", nullable: false),
                    ProcessedRecords = table.Column<int>(type: "int", nullable: false),
                    FlaggedRecords = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceBlobUrl = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadJobs", x => x.JobId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRecords_AnomalyScore",
                table: "TransactionRecords",
                column: "AnomalyScore");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRecords_FuelType",
                table: "TransactionRecords",
                column: "FuelType");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRecords_IsReviewed",
                table: "TransactionRecords",
                column: "IsReviewed");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRecords_MerchantState",
                table: "TransactionRecords",
                column: "MerchantState");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionRecords_TransactionDate",
                table: "TransactionRecords",
                column: "TransactionDate");

            migrationBuilder.CreateIndex(
                name: "IX_UploadJobs_Status",
                table: "UploadJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_UploadJobs_UploadedAt",
                table: "UploadJobs",
                column: "UploadedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionRecords");

            migrationBuilder.DropTable(
                name: "UploadJobs");
        }
    }
}
