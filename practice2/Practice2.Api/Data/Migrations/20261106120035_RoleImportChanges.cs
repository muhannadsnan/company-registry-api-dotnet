using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Practice2.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RoleImportChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_roles_identity",
                table: "roles");

            migrationBuilder.AddColumn<int>(
                name: "roles_saved",
                table: "import_runs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_roles_orgnr",
                table: "roles",
                column: "orgnr");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_roles_orgnr",
                table: "roles");

            migrationBuilder.DropColumn(
                name: "roles_saved",
                table: "import_runs");

            migrationBuilder.CreateIndex(
                name: "ux_roles_identity",
                table: "roles",
                columns: new[] { "orgnr", "role_type", "person_name", "birth_year", "unit_orgnr" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }
    }
}
