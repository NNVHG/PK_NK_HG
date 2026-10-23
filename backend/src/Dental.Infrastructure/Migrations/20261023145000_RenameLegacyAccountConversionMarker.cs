using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dental.Infrastructure.Migrations;

public partial class RenameLegacyAccountConversionMarker : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "IsLegacyFamilyAccount",
            table: "Users",
            newName: "RequiresIndividualIdentityConversion");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "RequiresIndividualIdentityConversion",
            table: "Users",
            newName: "IsLegacyFamilyAccount");
    }
}
