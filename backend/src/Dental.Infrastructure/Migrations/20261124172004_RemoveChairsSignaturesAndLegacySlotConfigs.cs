using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveChairsSignaturesAndLegacySlotConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "StaffWorkspaces" sw
                SET "RoomId" = dc."RoomId"
                FROM "DentalChairs" dc
                WHERE sw."RoomId" IS NULL
                  AND sw."ChairId" IS NOT NULL
                  AND dc."Id" = sw."ChairId"
                  AND dc."RoomId" IS NOT NULL;

                UPDATE "Visits" v
                SET "RoomId" = dc."RoomId"
                FROM "DentalChairs" dc
                WHERE v."RoomId" IS NULL
                  AND v."ChairId" IS NOT NULL
                  AND dc."Id" = v."ChairId"
                  AND dc."RoomId" IS NOT NULL;

                DELETE FROM "SystemConfigs"
                WHERE "ConfigKey" IN (
                    'Appointment:SlotDurationMinutes',
                    'Appointment:DefaultSlotDurationMinutes',
                    'Appointment:MaxAdvanceDays',
                    'Appointment:MaxParallelSlots',
                    'Appointment:OpeningTime',
                    'Appointment:ClosingTime'
                );

                INSERT INTO "SystemConfigs" ("ConfigKey", "ConfigValue", "Description", "DataType") VALUES
                    ('Clinic:WorkStartTime', '08:00', 'Giờ mở cửa tiếp nhận khám', 'String'),
                    ('Clinic:LunchStartTime', '12:00', 'Giờ bắt đầu nghỉ trưa', 'String'),
                    ('Clinic:LunchEndTime', '13:30', 'Giờ kết thúc nghỉ trưa', 'String'),
                    ('Clinic:WorkEndTime', '17:30', 'Giờ đóng cửa phòng khám', 'String')
                ON CONFLICT ("ConfigKey") DO NOTHING;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicRooms_DentalChairs_LegacyChairId",
                table: "ClinicRooms");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffWorkspaces_DentalChairs_ChairId",
                table: "StaffWorkspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Visits_DentalChairs_ChairId",
                table: "Visits");

            migrationBuilder.DropTable(
                name: "DentalChairs");

            migrationBuilder.DropTable(
                name: "DoctorSignatures");

            migrationBuilder.DropIndex(
                name: "IX_Visits_ActiveChair",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_StaffWorkspaces_ChairId",
                table: "StaffWorkspaces");

            migrationBuilder.DropIndex(
                name: "IX_ClinicRooms_LegacyChairId",
                table: "ClinicRooms");

            migrationBuilder.DropColumn(
                name: "ChairId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ChairId",
                table: "StaffWorkspaces");

            migrationBuilder.DropColumn(
                name: "LegacyChairId",
                table: "ClinicRooms");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChairId",
                table: "Visits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChairId",
                table: "StaffWorkspaces",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LegacyChairId",
                table: "ClinicRooms",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DentalChairs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChairNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RoomId = table.Column<int>(type: "integer", nullable: true),
                    RoomName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalChairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalChairs_ClinicRooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "ClinicRooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DentalChairs_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DentalChairs_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DoctorSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DoctorId = table.Column<int>(type: "integer", nullable: false),
                    ImagePng = table.Column<byte[]>(type: "bytea", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DoctorSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DoctorSignatures_Users_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DoctorSignatures_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ActiveChair",
                table: "Visits",
                column: "ChairId",
                unique: true,
                filter: "\"Status\" = 'InProgress' AND \"ChairId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_ChairId",
                table: "StaffWorkspaces",
                column: "ChairId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicRooms_LegacyChairId",
                table: "ClinicRooms",
                column: "LegacyChairId",
                unique: true,
                filter: "\"LegacyChairId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DentalChairs_ChairNumber",
                table: "DentalChairs",
                column: "ChairNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DentalChairs_CreatedByUserId",
                table: "DentalChairs",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalChairs_RoomId",
                table: "DentalChairs",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalChairs_UpdatedByUserId",
                table: "DentalChairs",
                column: "UpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSignatures_DoctorId_Version",
                table: "DoctorSignatures",
                columns: new[] { "DoctorId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DoctorSignatures_UpdatedByUserId",
                table: "DoctorSignatures",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicRooms_DentalChairs_LegacyChairId",
                table: "ClinicRooms",
                column: "LegacyChairId",
                principalTable: "DentalChairs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffWorkspaces_DentalChairs_ChairId",
                table: "StaffWorkspaces",
                column: "ChairId",
                principalTable: "DentalChairs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_DentalChairs_ChairId",
                table: "Visits",
                column: "ChairId",
                principalTable: "DentalChairs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
