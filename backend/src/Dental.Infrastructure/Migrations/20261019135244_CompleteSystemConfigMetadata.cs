using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dental.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSystemConfigMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_SystemConfigs",
                table: "SystemConfigs");

            migrationBuilder.RenameColumn(
                name: "Value",
                table: "SystemConfigs",
                newName: "ConfigValue");

            migrationBuilder.RenameColumn(
                name: "Key",
                table: "SystemConfigs",
                newName: "ConfigKey");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "SystemConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "DataType",
                table: "SystemConfigs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "String");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "SystemConfigs",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "SystemConfigs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedByUserId",
                table: "SystemConfigs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_SystemConfigs",
                table: "SystemConfigs",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_ConfigKey",
                table: "SystemConfigs",
                column: "ConfigKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemConfigs_UpdatedByUserId",
                table: "SystemConfigs",
                column: "UpdatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SystemConfigs_Users_UpdatedByUserId",
                table: "SystemConfigs",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SystemConfigs_Users_UpdatedByUserId",
                table: "SystemConfigs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_SystemConfigs",
                table: "SystemConfigs");

            migrationBuilder.DropIndex(
                name: "IX_SystemConfigs_ConfigKey",
                table: "SystemConfigs");

            migrationBuilder.DropIndex(
                name: "IX_SystemConfigs_UpdatedByUserId",
                table: "SystemConfigs");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "SystemConfigs");

            migrationBuilder.DropColumn(
                name: "DataType",
                table: "SystemConfigs");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "SystemConfigs");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "SystemConfigs");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "SystemConfigs");

            migrationBuilder.RenameColumn(
                name: "ConfigValue",
                table: "SystemConfigs",
                newName: "Value");

            migrationBuilder.RenameColumn(
                name: "ConfigKey",
                table: "SystemConfigs",
                newName: "Key");

            migrationBuilder.AddPrimaryKey(
                name: "PK_SystemConfigs",
                table: "SystemConfigs",
                column: "Key");
        }
    }
}
