using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleAssistantsPerDentist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffWorkspaces_DentistId",
                table: "StaffWorkspaces");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_DentistId",
                table: "StaffWorkspaces",
                column: "DentistId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StaffWorkspaces_DentistId",
                table: "StaffWorkspaces");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_DentistId",
                table: "StaffWorkspaces",
                column: "DentistId",
                unique: true);
        }
    }
}
