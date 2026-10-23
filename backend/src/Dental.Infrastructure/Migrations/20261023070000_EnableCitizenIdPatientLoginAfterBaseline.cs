using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations;

public partial class EnableCitizenIdPatientLoginAfterBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "LoginByCitizenId",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<bool>(
            name: "IsLegacyFamilyAccount",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("UPDATE \"Users\" SET \"IsLegacyFamilyAccount\" = TRUE WHERE \"UserId\" IN (SELECT \"UserId\" FROM \"Patients\" WHERE \"UserId\" IS NOT NULL GROUP BY \"UserId\" HAVING COUNT(*) > 1)");

        migrationBuilder.DropIndex(name: "IX_Users_Phone", table: "Users");
        migrationBuilder.CreateIndex(
            name: "IX_Users_Phone",
            table: "Users",
            column: "Phone",
            unique: true,
            filter: "\"LoginByCitizenId\" = false");

        migrationBuilder.DropIndex(name: "IX_Patients_Phone", table: "Patients");
        migrationBuilder.CreateIndex(
            name: "IX_Patients_Phone",
            table: "Patients",
            column: "Phone");

        migrationBuilder.DropIndex(name: "IX_Patients_UserId", table: "Patients");
        migrationBuilder.CreateIndex(
            name: "IX_Patients_UserId",
            table: "Patients",
            column: "UserId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Patients_UserId", table: "Patients");
        migrationBuilder.CreateIndex(
            name: "IX_Patients_UserId",
            table: "Patients",
            column: "UserId",
            unique: true);

        migrationBuilder.DropIndex(name: "IX_Patients_Phone", table: "Patients");
        migrationBuilder.CreateIndex(
            name: "IX_Patients_Phone",
            table: "Patients",
            column: "Phone",
            unique: true);

        migrationBuilder.DropIndex(name: "IX_Users_Phone", table: "Users");
        migrationBuilder.DropColumn(name: "LoginByCitizenId", table: "Users");
        migrationBuilder.DropColumn(name: "IsLegacyFamilyAccount", table: "Users");
        migrationBuilder.CreateIndex(
            name: "IX_Users_Phone",
            table: "Users",
            column: "Phone",
            unique: true);
    }
}
