using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tingle.Dependabot.Migrations;

/// <inheritdoc />
public partial class ImagesWithFullDigest : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UpdaterImageTag",
            table: "Projects");

        migrationBuilder.RenameColumn(
            name: "UpdaterImageDigest",
            table: "UpdateJobs",
            newName: "ProxyImage");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "ProxyImage",
            table: "UpdateJobs",
            newName: "UpdaterImageDigest");

        migrationBuilder.AddColumn<string>(
            name: "UpdaterImageTag",
            table: "Projects",
            type: "nvarchar(max)",
            nullable: true);
    }
}
