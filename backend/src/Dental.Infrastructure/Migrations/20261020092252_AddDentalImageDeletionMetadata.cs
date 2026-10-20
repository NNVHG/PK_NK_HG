using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDentalImageDeletionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeleteReason",
                table: "DentalImages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "DentalImages",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedByUserId",
                table: "DentalImages",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DentalImages_DeletedByUserId",
                table: "DentalImages",
                column: "DeletedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DentalImages_Users_DeletedByUserId",
                table: "DentalImages",
                column: "DeletedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DentalImages_Users_DeletedByUserId",
                table: "DentalImages");

            migrationBuilder.DropIndex(
                name: "IX_DentalImages_DeletedByUserId",
                table: "DentalImages");

            migrationBuilder.DropColumn(
                name: "DeleteReason",
                table: "DentalImages");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "DentalImages");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "DentalImages");
        }
    }
}
