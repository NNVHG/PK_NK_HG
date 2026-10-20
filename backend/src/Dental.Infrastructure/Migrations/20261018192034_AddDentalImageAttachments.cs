using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDentalImageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DentalImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    VisitId = table.Column<int>(type: "integer", nullable: true),
                    ToothNumber = table.Column<int>(type: "integer", nullable: true),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalImages_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "PatientId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DentalImages_Users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DentalImages_Visits_VisitId",
                        column: x => x.VisitId,
                        principalTable: "Visits",
                        principalColumn: "VisitId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DentalImages_PatientId_VisitId_CreatedAt",
                table: "DentalImages",
                columns: new[] { "PatientId", "VisitId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_DentalImages_StoragePath",
                table: "DentalImages",
                column: "StoragePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DentalImages_UploadedByUserId",
                table: "DentalImages",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalImages_VisitId",
                table: "DentalImages",
                column: "VisitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DentalImages");
        }
    }
}
