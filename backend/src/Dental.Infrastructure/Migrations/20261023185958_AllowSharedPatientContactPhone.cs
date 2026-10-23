using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowSharedPatientContactPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_Phone",
                table: "Patients");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Phone",
                table: "Patients",
                column: "Phone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_Phone",
                table: "Patients");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Phone",
                table: "Patients",
                column: "Phone",
                unique: true);
        }
    }
}
