using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDentistFromBookingAndAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Users_DentistId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_BookingRequests_Users_DentistId",
                table: "BookingRequests");

            migrationBuilder.DropIndex(
                name: "IX_BookingRequests_DentistId",
                table: "BookingRequests");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DentistId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "DentistId",
                table: "BookingRequests");

            migrationBuilder.DropColumn(
                name: "DentistId",
                table: "Appointments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DentistId",
                table: "BookingRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DentistId",
                table: "Appointments",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_DentistId",
                table: "BookingRequests",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DentistId",
                table: "Appointments",
                column: "DentistId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Users_DentistId",
                table: "Appointments",
                column: "DentistId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingRequests_Users_DentistId",
                table: "BookingRequests",
                column: "DentistId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
