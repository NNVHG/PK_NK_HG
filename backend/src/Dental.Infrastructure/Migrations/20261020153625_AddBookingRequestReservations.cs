using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingRequestReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BookingRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntentDigest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: false),
                    Gender = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AppointmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SlotTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    SlotDurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    DentistId = table.Column<int>(type: "integer", nullable: true),
                    VerifiedPatientId = table.Column<int>(type: "integer", nullable: true),
                    SubmittedByUserId = table.Column<int>(type: "integer", nullable: true),
                    IsReturning = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "integer", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingRequests", x => x.Id);
                    table.CheckConstraint("CK_BookingRequests_Duration", "\"SlotDurationMinutes\" BETWEEN 1 AND 1440");
                    table.CheckConstraint("CK_BookingRequests_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_BookingRequests_Patients_VerifiedPatientId",
                        column: x => x.VerifiedPatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRequests_Users_DentistId",
                        column: x => x.DentistId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRequests_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BookingRequests_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_ClientRequestId",
                table: "BookingRequests",
                column: "ClientRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_DentistId",
                table: "BookingRequests",
                column: "DentistId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_ReviewedByUserId",
                table: "BookingRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_Status_AppointmentDate_SlotTime",
                table: "BookingRequests",
                columns: new[] { "Status", "AppointmentDate", "SlotTime" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_SubmittedByUserId",
                table: "BookingRequests",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingRequests_VerifiedPatientId",
                table: "BookingRequests",
                column: "VerifiedPatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingRequests");
        }
    }
}
