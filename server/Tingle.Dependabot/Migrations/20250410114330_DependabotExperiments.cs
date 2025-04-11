using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tingle.Dependabot.Migrations;

/// <inheritdoc />
public partial class DependabotExperiments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Experiments",
            table: "Projects",
            type: "nvarchar(max)",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Experiments",
            table: "Projects");
    }
}
