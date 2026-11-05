using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Practice2.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    orgnr = table.Column<string>(type: "character(9)", fixedLength: true, maxLength: 9, nullable: false),
                    role_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    person_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    birth_year = table.Column<int>(type: "integer", nullable: true),
                    unit_orgnr = table.Column<string>(type: "character(9)", fixedLength: true, maxLength: 9, nullable: true),
                    unit_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    resigned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                    table.CheckConstraint("ck_roles_one_holder", "(person_name IS NULL) <> (unit_orgnr IS NULL)");
                    table.ForeignKey(
                        name: "fk_roles_companies_orgnr",
                        column: x => x.orgnr,
                        principalTable: "companies",
                        principalColumn: "orgnr",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_roles_birth_year",
                table: "roles",
                column: "birth_year");

            migrationBuilder.CreateIndex(
                name: "ix_roles_role_type",
                table: "roles",
                column: "role_type");

            migrationBuilder.CreateIndex(
                name: "ux_roles_identity",
                table: "roles",
                columns: new[] { "orgnr", "role_type", "person_name", "birth_year", "unit_orgnr" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
