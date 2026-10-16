using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhysicalVisitChairAllocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChairId",
                table: "Visits",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visits_ActiveChair",
                table: "Visits",
                column: "ChairId",
                unique: true,
                filter: "\"Status\" = 'InProgress' AND \"ChairId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Visits_DentalChairs_ChairId",
                table: "Visits",
                column: "ChairId",
                principalTable: "DentalChairs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Visits_DentalChairs_ChairId",
                table: "Visits");

            migrationBuilder.DropIndex(
                name: "IX_Visits_ActiveChair",
                table: "Visits");

            migrationBuilder.DropColumn(
                name: "ChairId",
                table: "Visits");
        }
    }
}
