using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tingle.Dependabot.Migrations;

/// <inheritdoc />
public partial class RemoveLocation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Location",
            table: "Projects");

        migrationBuilder.AddColumn<bool>(
            name: "Debug",
            table: "Projects",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Debug",
            table: "Projects");

        migrationBuilder.AddColumn<string>(
            name: "Location",
            table: "Projects",
            type: "nvarchar(max)",
            nullable: true);
    }
}
