using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCounterPatientPhoneRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PatientPhoneChangeRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AppointmentId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CurrentPhone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    NewPhone = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TokenVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientPhoneChangeRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientPhoneChangeRequests_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "AppointmentId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientPhoneChangeRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PatientPhoneChangeRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPhoneChangeRequests_AppointmentId",
                table: "PatientPhoneChangeRequests",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPhoneChangeRequests_CreatedByUserId",
                table: "PatientPhoneChangeRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPhoneChangeRequests_UserId_TokenVersion",
                table: "PatientPhoneChangeRequests",
                columns: new[] { "UserId", "TokenVersion" },
                unique: true,
                filter: "\"CompletedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PatientPhoneChangeRequests");
        }
    }
}
