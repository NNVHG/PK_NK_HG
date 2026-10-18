using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceRefundProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceRefundProposals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvoiceId = table.Column<int>(type: "integer", nullable: false),
                    Snapshot = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ReasonNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ReviewerId = table.Column<int>(type: "integer", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RefundId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRefundProposals", x => x.Id);
                    table.CheckConstraint("CK_InvoiceRefundProposals_Status", "\"Status\" IN ('Pending','Approved','Rejected','Stale')");
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposals_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposals_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposals_Users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceRefundProposalItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProposalId = table.Column<int>(type: "integer", nullable: false),
                    InvoiceItemId = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceRefundProposalItems", x => x.Id);
                    table.CheckConstraint("CK_InvoiceRefundProposalItems_Quantity", "\"Quantity\" > 0");
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposalItems_InvoiceItems_InvoiceItemId",
                        column: x => x.InvoiceItemId,
                        principalTable: "InvoiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceRefundProposalItems_InvoiceRefundProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "InvoiceRefundProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposalItems_InvoiceItemId",
                table: "InvoiceRefundProposalItems",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposalItems_ProposalId_InvoiceItemId",
                table: "InvoiceRefundProposalItems",
                columns: new[] { "ProposalId", "InvoiceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_CreatorId",
                table: "InvoiceRefundProposals",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_InvoiceId",
                table: "InvoiceRefundProposals",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_RequestId",
                table: "InvoiceRefundProposals",
                column: "RequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_ReviewerId",
                table: "InvoiceRefundProposals",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceRefundProposals_Status_CreatedAt",
                table: "InvoiceRefundProposals",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceRefundProposalItems");

            migrationBuilder.DropTable(
                name: "InvoiceRefundProposals");
        }
    }
}
