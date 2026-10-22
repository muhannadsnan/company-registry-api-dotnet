using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CompanyRegistry.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShortenCompanyColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "organisation_form_description",
                table: "companies");

            migrationBuilder.RenameColumn(
                name: "organisation_form_code",
                table: "companies",
                newName: "form_code");

            migrationBuilder.RenameColumn(
                name: "organisation_number",
                table: "companies",
                newName: "orgnr");

            migrationBuilder.RenameIndex(
                name: "ix_companies_organisation_form_code",
                table: "companies",
                newName: "ix_companies_form_code");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "form_code",
                table: "companies",
                newName: "organisation_form_code");

            migrationBuilder.RenameColumn(
                name: "orgnr",
                table: "companies",
                newName: "organisation_number");

            migrationBuilder.RenameIndex(
                name: "ix_companies_form_code",
                table: "companies",
                newName: "ix_companies_organisation_form_code");

            migrationBuilder.AddColumn<string>(
                name: "organisation_form_description",
                table: "companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }
    }
}
