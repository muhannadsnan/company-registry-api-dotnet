using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Practice.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrelationIdToImportRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                table: "import_runs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "correlation_id",
                table: "import_runs");
        }
    }
}
