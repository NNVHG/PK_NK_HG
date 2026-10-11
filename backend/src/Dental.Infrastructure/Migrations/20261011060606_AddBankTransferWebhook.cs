using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBankTransferWebhook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BankReceivedAmount",
                table: "PaymentTransactions",
                type: "numeric(18,0)",
                precision: 18,
                scale: 0,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "PaymentTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "PaymentTransactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionReference",
                table: "PaymentTransactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemConfigs",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemConfigs", x => x.Key);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Source_TransactionReference",
                table: "PaymentTransactions",
                columns: new[] { "Source", "TransactionReference" },
                unique: true,
                filter: "\"TransactionReference\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemConfigs");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_Source_TransactionReference",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "BankReceivedAmount",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "TransactionReference",
                table: "PaymentTransactions");
        }
    }
}
