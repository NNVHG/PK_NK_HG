using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceRefundHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RefundReasonCode",
                table: "PaymentTransactions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefundReasonNotes",
                table: "PaymentTransactions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionType",
                table: "PaymentTransactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Receipt");

            migrationBuilder.AddColumn<int>(
                name: "RefundRevision",
                table: "Invoices",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "InvoiceRefunds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentTransactionId = table.Column<int>(type: "integer", nullable: true),
                    RefundAmount = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    DebtReduction = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    TotalBefore = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    DiscountBefore = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    PaidBefore = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    TotalAfter = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    DiscountAfter = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    PaidAfter = table.Column<decimal>(type: "numeric(18,0)", precision: 18, scale: 0, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReasonNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PaymentMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    LateApproval = table.Column<bool>(type: "boolean", nullable: false),
                    ReserveConfirmed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRefunds", x => x.Id);
                    table.CheckConstraint("CK_InvoiceRefunds_Amounts", "\"RefundAmount\" >= 0 AND \"DebtReduction\" >= 0 AND \"TotalAfter\" >= \"DiscountAfter\" AND \"PaidAfter\" >= 0 AND \"PaidAfter\" <= \"TotalAfter\" - \"DiscountAfter\"");
                    table.CheckConstraint("CK_InvoiceRefunds_Payment", "(\"RefundAmount\" = 0 AND \"PaymentTransactionId\" IS NULL) OR (\"RefundAmount\" > 0 AND \"PaymentTransactionId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_InvoiceRefunds_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefunds_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefunds_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceRefundItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceRefundId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRefundItems", x => x.Id);
                    table.CheckConstraint("CK_InvoiceRefundItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_InvoiceRefundItems_InvoiceItems_InvoiceItemId",
                        column: x => x.InvoiceItemId,
                        principalTable: "InvoiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefundItems_InvoiceRefunds_InvoiceRefundId",
                        column: x => x.InvoiceRefundId,
                        principalTable: "InvoiceRefunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundItems_InvoiceItemId",
                table: "InvoiceRefundItems",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundItems_InvoiceRefundId_InvoiceItemId",
                table: "InvoiceRefundItems",
                columns: new[] { "InvoiceRefundId", "InvoiceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_ActorId",
                table: "InvoiceRefunds",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_InvoiceId_Revision",
                table: "InvoiceRefunds",
                columns: new[] { "InvoiceId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_PaymentTransactionId",
                table: "InvoiceRefunds",
                column: "PaymentTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefunds_RequestId",
                table: "InvoiceRefunds",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceRefundItems");

            migrationBuilder.DropTable(
                name: "InvoiceRefunds");

            migrationBuilder.DropColumn(
                name: "RefundReasonCode",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RefundReasonNotes",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionType",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "RefundRevision",
                table: "Invoices");
        }
    }
}
