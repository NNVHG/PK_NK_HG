using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceDiscounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Invoices",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "DiscountPresetId",
                table: "Invoices",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountReason",
                table: "Invoices",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscountType",
                table: "Invoices",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountValue",
                table: "Invoices",
                type: "numeric(20,2)",
                precision: 20,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DiscountPresets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DiscountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscountPresets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DiscountPresetId",
                table: "Invoices",
                column: "DiscountPresetId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Invoices_DiscountAmount",
                table: "Invoices",
                sql: "\"DiscountAmount\" >= 0 AND \"DiscountAmount\" <= \"TotalAmount\"");

            migrationBuilder.CreateIndex(
                name: "IX_DiscountPresets_Code",
                table: "DiscountPresets",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_DiscountPresets_DiscountPresetId",
                table: "Invoices",
                column: "DiscountPresetId",
                principalTable: "DiscountPresets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_DiscountPresets_DiscountPresetId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "DiscountPresets");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_DiscountPresetId",
                table: "Invoices");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Invoices_DiscountAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountPresetId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountType",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "DiscountValue",
                table: "Invoices");
        }
    }
}
