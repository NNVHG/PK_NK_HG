using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDentalImageClinicalNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClinicalNotes",
                table: "DentalImages",
                type: "text",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClinicalUpdatedAt",
                table: "DentalImages",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClinicalUpdatedByUserId",
                table: "DentalImages",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoctorDiagnosis",
                table: "DentalImages",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DentalImageClinicalAddenda",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DentalImageId = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AuthorRole = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DentalImageClinicalAddenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DentalImageClinicalAddenda_DentalImages_DentalImageId",
                        column: x => x.DentalImageId,
                        principalTable: "DentalImages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DentalImageClinicalAddenda_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DentalImages_ClinicalUpdatedByUserId",
                table: "DentalImages",
                column: "ClinicalUpdatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalImageClinicalAddenda_CreatedByUserId",
                table: "DentalImageClinicalAddenda",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DentalImageClinicalAddenda_DentalImageId_Id",
                table: "DentalImageClinicalAddenda",
                columns: new[] { "DentalImageId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DentalImageClinicalAddenda_RequestId",
                table: "DentalImageClinicalAddenda",
                column: "RequestId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DentalImages_Users_ClinicalUpdatedByUserId",
                table: "DentalImages",
                column: "ClinicalUpdatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DentalImages_Users_ClinicalUpdatedByUserId",
                table: "DentalImages");

            migrationBuilder.DropTable(
                name: "DentalImageClinicalAddenda");

            migrationBuilder.DropIndex(
                name: "IX_DentalImages_ClinicalUpdatedByUserId",
                table: "DentalImages");

            migrationBuilder.DropColumn(
                name: "ClinicalNotes",
                table: "DentalImages");

            migrationBuilder.DropColumn(
                name: "ClinicalUpdatedAt",
                table: "DentalImages");

            migrationBuilder.DropColumn(
                name: "ClinicalUpdatedByUserId",
                table: "DentalImages");

            migrationBuilder.DropColumn(
                name: "DoctorDiagnosis",
                table: "DentalImages");
        }
    }
}
