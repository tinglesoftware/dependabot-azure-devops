using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tingle.Dependabot.Migrations;

/// <inheritdoc />
public partial class AddProjectUserId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_DataProtectionKeys",
            table: "DataProtectionKeys");

        migrationBuilder.RenameTable(
            name: "DataProtectionKeys",
            newName: "Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys");

        migrationBuilder.AddColumn<string>(
            name: "UserId",
            table: "Projects",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddPrimaryKey(
            name: "PK_Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys",
            table: "Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys",
            column: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(
            name: "PK_Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys",
            table: "Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "Projects");

        migrationBuilder.RenameTable(
            name: "Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys",
            newName: "DataProtectionKeys");

        migrationBuilder.AddPrimaryKey(
            name: "PK_DataProtectionKeys",
            table: "DataProtectionKeys",
            column: "Id");
    }
}
