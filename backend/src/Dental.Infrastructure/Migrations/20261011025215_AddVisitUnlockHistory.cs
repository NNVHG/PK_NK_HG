using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitUnlockHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LockedAt",
                table: "Visits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LockedBy",
                table: "Visits",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VisitUnlockRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
                    ActorUserId = table.Column<int>(type: "integer", nullable: false),
                    CancelledInvoiceId = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitUnlockRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitUnlockRecords_Invoices_CancelledInvoiceId",
                        column: x => x.CancelledInvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitUnlockRecords_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VisitUnlockRecords_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "VisitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VisitUnlockRecords_ActorUserId",
                table: "VisitUnlockRecords",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitUnlockRecords_CancelledInvoiceId",
                table: "VisitUnlockRecords",
                column: "CancelledInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitUnlockRecords_VisitId",
                table: "VisitUnlockRecords",
                column: "VisitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VisitUnlockRecords");

            migrationBuilder.DropColumn(
                name: "LockedAt",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "Visits");
        }
    }
}
