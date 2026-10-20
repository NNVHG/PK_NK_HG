using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentDurationSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlotDurationMinutes",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Appointments_Duration",
                table: "Appointments",
                sql: "\"SlotDurationMinutes\" BETWEEN 1 AND 1440");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Appointments_Duration",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "SlotDurationMinutes",
                table: "Appointments");
        }
    }
}
