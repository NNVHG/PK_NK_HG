using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhoneActivationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhoneActivationReason",
                table: "Users",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhoneActivationReason",
                table: "Users");
        }
    }
}
