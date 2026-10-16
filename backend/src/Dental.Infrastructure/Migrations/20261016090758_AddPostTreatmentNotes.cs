using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostTreatmentNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostTreatmentNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VisitId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostTreatmentNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostTreatmentNotes_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PostTreatmentNotes_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "VisitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostTreatmentNotes_CreatedByUserId",
                table: "PostTreatmentNotes",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PostTreatmentNotes_RequestId",
                table: "PostTreatmentNotes",
                column: "RequestId",
                unique: true,
                filter: "\"RequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PostTreatmentNotes_VisitId_CreatedAt",
                table: "PostTreatmentNotes",
                columns: new[] { "VisitId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostTreatmentNotes");
        }
    }
}
