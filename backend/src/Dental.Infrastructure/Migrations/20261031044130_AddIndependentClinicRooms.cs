using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndependentClinicRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "Visits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "StaffWorkspaces",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "WorkDate",
                table: "StaffWorkspaces",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "DentalChairs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClinicRooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RoomName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalizedRoomName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LegacyChairId = table.Column<int>(type: "integer", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicRooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClinicRooms_DentalChairs_LegacyChairId",
                        column: x => x.LegacyChairId,
                        principalTable: "DentalChairs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicRooms_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClinicRooms_Users_UpdatedByUserId",
                        column: x => x.UpdatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ActiveRoom",
                table: "Visits",
                column: "RoomId",
                unique: true,
                filter: "\"Status\" = 'InProgress' AND \"RoomId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StaffWorkspaces_RoomId",
                table: "StaffWorkspaces",
                column: "RoomId",
                unique: true,
                filter: "\"RoomId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DentalChairs_RoomId",
                table: "DentalChairs",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicRooms_CreatedByUserId",
                table: "ClinicRooms",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicRooms_LegacyChairId",
                table: "ClinicRooms",
                column: "LegacyChairId",
                unique: true,
                filter: "\"LegacyChairId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicRooms_NormalizedRoomName",
                table: "ClinicRooms",
                column: "NormalizedRoomName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicRooms_RoomCode",
                table: "ClinicRooms",
                column: "RoomCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicRooms_RoomName",
                table: "ClinicRooms",
                column: "RoomName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClinicRooms_UpdatedByUserId",
                table: "ClinicRooms",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DentalChairs_ClinicRooms_RoomId",
                table: "DentalChairs",
                column: "RoomId",
                principalTable: "ClinicRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StaffWorkspaces_ClinicRooms_RoomId",
                table: "StaffWorkspaces",
                column: "RoomId",
                principalTable: "ClinicRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_ClinicRooms_RoomId",
                table: "Visits",
                column: "RoomId",
                principalTable: "ClinicRooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "DentalChairs") THEN
                        IF EXISTS (
                            SELECT 1
                            FROM "DentalChairs"
                            WHERE BTRIM(COALESCE("RoomName", '')) = ''
                               OR BTRIM(COALESCE("ChairNumber", '')) = ''
                        ) THEN
                            RAISE EXCEPTION 'Preflight dữ liệu phòng thất bại: có ghế thiếu tên phòng hoặc mã ghế.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM "DentalChairs"
                            GROUP BY UPPER(REGEXP_REPLACE(BTRIM("RoomName"), '\s+', ' ', 'g'))
                            HAVING COUNT(*) > 1
                        ) THEN
                            RAISE EXCEPTION 'Preflight dữ liệu phòng thất bại: nhiều ghế trùng tên phòng, không tự chọn phòng.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM "Visits" v
                            JOIN "DentalChairs" c ON c."Id" = v."ChairId"
                            WHERE v."Status" = 'InProgress'
                              AND (c."IsActive" = FALSE OR c."Status" IN (3, 4))
                        ) THEN
                            RAISE EXCEPTION 'Preflight dữ liệu phòng thất bại: ca đang khám gắn với ghế bảo trì hoặc ngừng sử dụng.';
                        END IF;

                        IF EXISTS (
                            SELECT 1
                            FROM "StaffWorkspaces"
                            WHERE "ChairId" IS NOT NULL AND "DentistId" IS NOT NULL
                        ) THEN
                            RAISE EXCEPTION 'Preflight dữ liệu phòng thất bại: vị trí làm việc vừa gắn ghế vừa gắn nha sĩ.';
                        END IF;

                        INSERT INTO "ClinicRooms" (
                            "RoomCode",
                            "RoomName",
                            "NormalizedRoomName",
                            "Status",
                            "Description",
                            "IsActive",
                            "LegacyChairId",
                            "CreatedByUserId",
                            "UpdatedByUserId",
                            "Version",
                            "CreatedAt",
                            "UpdatedAt"
                        )
                        SELECT
                            UPPER(BTRIM(c."ChairNumber")),
                            REGEXP_REPLACE(BTRIM(c."RoomName"), '\s+', ' ', 'g'),
                            UPPER(REGEXP_REPLACE(BTRIM(c."RoomName"), '\s+', ' ', 'g')),
                            c."Status",
                            c."Description",
                            c."IsActive",
                            c."Id",
                            c."CreatedByUserId",
                            c."UpdatedByUserId",
                            c."Version",
                            c."CreatedAt",
                            c."UpdatedAt"
                        FROM "DentalChairs" c
                        WHERE NOT EXISTS (
                            SELECT 1 FROM "ClinicRooms" r WHERE r."LegacyChairId" = c."Id"
                        )
                        ORDER BY c."Id";

                        UPDATE "DentalChairs" c
                        SET "RoomId" = r."Id"
                        FROM "ClinicRooms" r
                        WHERE r."LegacyChairId" = c."Id"
                          AND c."RoomId" IS NULL;

                        UPDATE "StaffWorkspaces" w
                        SET "RoomId" = c."RoomId"
                        FROM "DentalChairs" c
                        WHERE w."ChairId" = c."Id"
                          AND c."RoomId" IS NOT NULL
                          AND w."RoomId" IS NULL;

                        UPDATE "Visits" v
                        SET "RoomId" = c."RoomId"
                        FROM "DentalChairs" c
                        WHERE v."ChairId" = c."Id"
                          AND c."RoomId" IS NOT NULL
                          AND v."RoomId" IS NULL;
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DentalChairs_ClinicRooms_RoomId",
                table: "DentalChairs");

            migrationBuilder.DropForeignKey(
                name: "FK_StaffWorkspaces_ClinicRooms_RoomId",
                table: "StaffWorkspaces");

            migrationBuilder.DropForeignKey(
                name: "FK_Visits_ClinicRooms_RoomId",
                table: "Visits");

            migrationBuilder.DropTable(
                name: "ClinicRooms");

            migrationBuilder.DropIndex(
                name: "IX_Visits_ActiveRoom",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_StaffWorkspaces_RoomId",
                table: "StaffWorkspaces");

            migrationBuilder.DropIndex(
                name: "IX_DentalChairs_RoomId",
                table: "DentalChairs");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "StaffWorkspaces");

            migrationBuilder.DropColumn(
                name: "WorkDate",
                table: "StaffWorkspaces");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "DentalChairs");
        }
    }
}
