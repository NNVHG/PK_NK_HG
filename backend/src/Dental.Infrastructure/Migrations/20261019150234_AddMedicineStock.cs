using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicineStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicineStockBalances",
                columns: table => new
                {
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    OnHand = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineStockBalances", x => x.MedicineId);
                    table.CheckConstraint("CK_MedicineStockBalance_Nonnegative", "\"OnHand\" >= 0");
                    table.ForeignKey(
                        name: "FK_MedicineStockBalances_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineStockIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrescriptionId = table.Column<int>(type: "integer", nullable: false),
                    PrescriptionItemId = table.Column<int>(type: "integer", nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    MedicineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    DispenseEventId = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedByUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineStockIssues", x => x.Id);
                    table.CheckConstraint("CK_MedicineStockIssue_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_PrescriptionDispenseEvents_DispenseEven~",
                        column: x => x.DispenseEventId,
                        principalTable: "PrescriptionDispenseEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_PrescriptionItems_PrescriptionItemId",
                        column: x => x.PrescriptionItemId,
                        principalTable: "PrescriptionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockIssues_Users_IssuedByUserId",
                        column: x => x.IssuedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MedicineStockReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MedicineId = table.Column<int>(type: "integer", nullable: false),
                    MedicineCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MedicineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SupplierName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConfirmedByUserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineStockReceipts", x => x.Id);
                    table.CheckConstraint("CK_MedicineStockReceipt_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_MedicineStockReceipts_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MedicineStockReceipts_Users_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_DispenseEventId",
                table: "MedicineStockIssues",
                column: "DispenseEventId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_IssuedByUserId",
                table: "MedicineStockIssues",
                column: "IssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_MedicineId",
                table: "MedicineStockIssues",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_PrescriptionId_MedicineId",
                table: "MedicineStockIssues",
                columns: new[] { "PrescriptionId", "MedicineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockIssues_PrescriptionItemId",
                table: "MedicineStockIssues",
                column: "PrescriptionItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockReceipts_ConfirmedByUserId",
                table: "MedicineStockReceipts",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockReceipts_MedicineId_Id",
                table: "MedicineStockReceipts",
                columns: new[] { "MedicineId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicineStockReceipts_RequestId",
                table: "MedicineStockReceipts",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicineStockBalances");

            migrationBuilder.DropTable(
                name: "MedicineStockIssues");

            migrationBuilder.DropTable(
                name: "MedicineStockReceipts");
        }
    }
}
