using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientIdentityMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "Patients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HashScheme",
                table: "Patients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdentityKeyVersion",
                table: "Patients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IssuingCountryCode",
                table: "Patients",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HashScheme",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "IdentityKeyVersion",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "IssuingCountryCode",
                table: "Patients");
        }
    }
}
