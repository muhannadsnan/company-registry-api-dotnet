using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Practice2.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixImportRunColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "finishedt",
                table: "import_runs",
                newName: "finished_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "finished_at",
                table: "import_runs",
                newName: "finishedt");
        }
    }
}
