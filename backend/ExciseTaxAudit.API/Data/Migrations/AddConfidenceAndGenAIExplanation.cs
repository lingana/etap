using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExciseTaxAudit.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConfidenceAndGenAIExplanation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "Confidence",
                table: "TransactionRecords",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GenAIExplanation",
                table: "TransactionRecords",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confidence",
                table: "TransactionRecords");

            migrationBuilder.DropColumn(
                name: "GenAIExplanation",
                table: "TransactionRecords");
        }
    }
}
