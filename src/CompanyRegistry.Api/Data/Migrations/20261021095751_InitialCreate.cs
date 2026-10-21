using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CompanyRegistry.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    organisation_number = table.Column<string>(type: "character(9)", fixedLength: true, maxLength: 9, nullable: false),
                    name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    organisation_form_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    organisation_form_description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    street_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    postal_place = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    municipality_number = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    municipality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    registration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    first_imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.organisation_number);
                });

            migrationBuilder.CreateTable(
                name: "import_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    trigger = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rows_read = table.Column<int>(type: "integer", nullable: false),
                    rows_inserted = table.Column<int>(type: "integer", nullable: false),
                    rows_updated = table.Column<int>(type: "integer", nullable: false),
                    rows_unchanged = table.Column<int>(type: "integer", nullable: false),
                    rows_rejected = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_companies_municipality_number",
                table: "companies",
                column: "municipality_number");

            migrationBuilder.CreateIndex(
                name: "ix_companies_organisation_form_code",
                table: "companies",
                column: "organisation_form_code");

            migrationBuilder.CreateIndex(
                name: "ix_companies_postal_code",
                table: "companies",
                column: "postal_code");

            migrationBuilder.CreateIndex(
                name: "ix_import_runs_queued_at",
                table: "import_runs",
                column: "queued_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "import_runs");
        }
    }
}
