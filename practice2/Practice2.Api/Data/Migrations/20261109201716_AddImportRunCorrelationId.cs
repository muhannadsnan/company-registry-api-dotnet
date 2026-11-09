using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Practice2.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImportRunCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "correlation_id",
                table: "import_runs",
                type: "character varying(64)",
                maxLength: 64,
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
