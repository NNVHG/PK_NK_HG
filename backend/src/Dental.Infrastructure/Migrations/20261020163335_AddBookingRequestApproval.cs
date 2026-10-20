using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRequestApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresPhoneActivation",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedAppointmentId",
                table: "BookingRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CreatedAccount",
                table: "BookingRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_ApprovedAppointmentId",
                table: "BookingRequests",
                column: "ApprovedAppointmentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BookingRequests_Appointments_ApprovedAppointmentId",
                table: "BookingRequests",
                column: "ApprovedAppointmentId",
                principalTable: "Appointments",
                principalColumn: "AppointmentId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookingRequests_Appointments_ApprovedAppointmentId",
                table: "BookingRequests");

            migrationBuilder.DropIndex(
                name: "IX_BookingRequests_ApprovedAppointmentId",
                table: "BookingRequests");

            migrationBuilder.DropColumn(
                name: "RequiresPhoneActivation",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ApprovedAppointmentId",
                table: "BookingRequests");

            migrationBuilder.DropColumn(
                name: "CreatedAccount",
                table: "BookingRequests");
        }
    }
}
