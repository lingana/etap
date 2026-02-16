using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExciseTaxAudit.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComplianceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add compliance workflow fields to TransactionRecords
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TransactionRecords",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "FLAGGED");

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "TransactionRecords",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserName",
                table: "TransactionRecords",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "TransactionRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "TransactionRecords",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserName",
                table: "TransactionRecords",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "TransactionRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimSchedule",
                table: "TransactionRecords",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TaxPeriod",
                table: "TransactionRecords",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ClaimAmount",
                table: "TransactionRecords",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLocked",
                table: "TransactionRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrls",
                table: "TransactionRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "TransactionRecords",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            // Create AuditLogs table
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EngagementId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UserRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Comments = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            // Create TransactionAttachments table
            migrationBuilder.CreateTable(
                name: "TransactionAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransactionId = table.Column<int>(type: "int", nullable: false),
                    EngagementId = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionAttachments", x => x.Id);
                });

            // Create ExportHistories table
            migrationBuilder.CreateTable(
                name: "ExportHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EngagementId = table.Column<int>(type: "int", nullable: false),
                    ExportType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExportedBy = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ExportedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    Parameters = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportHistories", x => x.Id);
                });

            // Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EngagementId",
                table: "AuditLogs",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityId",
                table: "AuditLogs",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttachments_TransactionId",
                table: "TransactionAttachments",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAttachments_EngagementId",
                table: "TransactionAttachments",
                column: "EngagementId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportHistories_EngagementId",
                table: "ExportHistories",
                column: "EngagementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AuditLogs");
            migrationBuilder.DropTable(name: "TransactionAttachments");
            migrationBuilder.DropTable(name: "ExportHistories");

            migrationBuilder.DropColumn(name: "Status", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "ReviewedByUserId", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "ReviewedByUserName", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "ReviewedAt", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "ApprovedByUserId", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "ApprovedByUserName", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "ApprovedAt", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "ClaimSchedule", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "TaxPeriod", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "ClaimAmount", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "IsLocked", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "AttachmentUrls", table: "TransactionRecords");
            migrationBuilder.DropColumn(name: "RejectionReason", table: "TransactionRecords");
        }
    }
}
