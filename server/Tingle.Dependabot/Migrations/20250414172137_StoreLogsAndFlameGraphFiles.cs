using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tingle.Dependabot.Migrations;

/// <inheritdoc />
public partial class StoreLogsAndFlameGraphFiles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "Log",
            table: "UpdateJobs",
            newName: "LogsPath");

        migrationBuilder.AddColumn<string>(
            name: "FlameGraphPath",
            table: "UpdateJobs",
            type: "nvarchar(max)",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FlameGraphPath",
            table: "UpdateJobs");

        migrationBuilder.RenameColumn(
            name: "LogsPath",
            table: "UpdateJobs",
            newName: "Log");
    }
}
