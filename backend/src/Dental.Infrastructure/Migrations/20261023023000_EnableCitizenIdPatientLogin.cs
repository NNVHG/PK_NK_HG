using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations;

[DbContext(typeof(DentalDbContext))]
[Migration("20261023023000_EnableCitizenIdPatientLogin")]
public partial class EnableCitizenIdPatientLogin : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(name: "LoginByCitizenId", table: "Users", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<bool>(name: "IsLegacyFamilyAccount", table: "Users", type: "boolean", nullable: false, defaultValue: false);
        migrationBuilder.Sql("UPDATE \"Users\" SET \"IsLegacyFamilyAccount\" = TRUE WHERE \"UserId\" IN (SELECT \"UserId\" FROM \"Patients\" WHERE \"UserId\" IS NOT NULL GROUP BY \"UserId\" HAVING COUNT(*) > 1)");
        migrationBuilder.DropIndex(name: "IX_Users_Phone", table: "Users");
        migrationBuilder.CreateIndex(name: "IX_Users_Phone", table: "Users", column: "Phone", unique: true,
            filter: "\"LoginByCitizenId\" = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_Phone", table: "Users");
        migrationBuilder.DropColumn(name: "LoginByCitizenId", table: "Users");
        migrationBuilder.DropColumn(name: "IsLegacyFamilyAccount", table: "Users");
        migrationBuilder.CreateIndex(name: "IX_Users_Phone", table: "Users", column: "Phone", unique: true);
    }
}
