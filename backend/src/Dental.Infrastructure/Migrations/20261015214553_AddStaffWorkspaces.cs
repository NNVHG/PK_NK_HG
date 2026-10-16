using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StaffWorkspaces",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ChairId = table.Column<int>(type: "integer", nullable: true),
                    DentistId = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StaffWorkspaces", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_StaffWorkspaces_DentalChairs_ChairId",
                        column: x => x.ChairId,
                        principalTable: "DentalChairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffWorkspaces_Users_DentistId",
                        column: x => x.DentistId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffWorkspaces_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StaffWorkspaces_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_ChairId",
                table: "StaffWorkspaces",
                column: "ChairId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_DentistId",
                table: "StaffWorkspaces",
                column: "DentistId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_UpdatedByUserId",
                table: "StaffWorkspaces",
                column: "UpdatedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StaffWorkspaces");
        }
    }
}
