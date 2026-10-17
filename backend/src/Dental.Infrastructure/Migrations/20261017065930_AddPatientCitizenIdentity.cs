using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientCitizenIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CitizenIdHash",
                table: "Patients",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CitizenIdMasked",
                table: "Patients",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CitizenIdVersion",
                table: "Patients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_CitizenIdHash",
                table: "Patients",
                column: "CitizenIdHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_CitizenIdHash",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "CitizenIdHash",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "CitizenIdMasked",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "CitizenIdVersion",
                table: "Patients");
        }
    }
}
