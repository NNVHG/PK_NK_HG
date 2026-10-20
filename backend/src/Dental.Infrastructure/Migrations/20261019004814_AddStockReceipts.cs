using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStockReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StockBalances",
                columns: table => new
                {
                    MedicalSupplyId = table.Column<int>(type: "integer", nullable: false),
                    OnHand = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockBalances", x => x.MedicalSupplyId);
                    table.CheckConstraint("CK_StockBalance_OnHand", "\"OnHand\" >= 0");
                    table.ForeignKey(
                        name: "FK_StockBalances_MedicalSupplies_MedicalSupplyId",
                        column: x => x.MedicalSupplyId,
                        principalTable: "MedicalSupplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockReceipts_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockReceiptLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StockReceiptId = table.Column<int>(type: "integer", nullable: false),
                    MedicalSupplyId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SupplyCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SupplyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockReceiptLines", x => x.Id);
                    table.CheckConstraint("CK_StockReceiptLine_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_StockReceiptLines_MedicalSupplies_MedicalSupplyId",
                        column: x => x.MedicalSupplyId,
                        principalTable: "MedicalSupplies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockReceiptLines_StockReceipts_StockReceiptId",
                        column: x => x.StockReceiptId,
                        principalTable: "StockReceipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptLines_MedicalSupplyId",
                table: "StockReceiptLines",
                column: "MedicalSupplyId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceiptLines_StockReceiptId_MedicalSupplyId",
                table: "StockReceiptLines",
                columns: new[] { "StockReceiptId", "MedicalSupplyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_ConfirmedByUserId",
                table: "StockReceipts",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StockReceipts_RequestId",
                table: "StockReceipts",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StockBalances");

            migrationBuilder.DropTable(
                name: "StockReceiptLines");

            migrationBuilder.DropTable(
                name: "StockReceipts");
        }
    }
}
