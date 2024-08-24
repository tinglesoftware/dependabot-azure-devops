using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tingle.Dependabot.Migrations;

/// <inheritdoc />
public partial class DirectoriesAndGroups : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory",
            table: "UpdateJobs");

        migrationBuilder.DropIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory_EventBusId",
            table: "UpdateJobs");

        migrationBuilder.AlterColumn<string>(
            name: "Directory",
            table: "UpdateJobs",
            type: "nvarchar(450)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(450)");

        migrationBuilder.AddColumn<string>(
            name: "Directories",
            table: "UpdateJobs",
            type: "nvarchar(450)",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory_Directories",
            table: "UpdateJobs",
            columns: new[] { "PackageEcosystem", "Directory", "Directories" });

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory_Directories_EventBusId",
            table: "UpdateJobs",
            columns: new[] { "PackageEcosystem", "Directory", "Directories", "EventBusId" },
            unique: true,
            filter: "[Directory] IS NOT NULL AND [Directories] IS NOT NULL AND [EventBusId] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory_Directories",
            table: "UpdateJobs");

        migrationBuilder.DropIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory_Directories_EventBusId",
            table: "UpdateJobs");

        migrationBuilder.DropColumn(
            name: "Directories",
            table: "UpdateJobs");

        migrationBuilder.AlterColumn<string>(
            name: "Directory",
            table: "UpdateJobs",
            type: "nvarchar(450)",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "nvarchar(450)",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory",
            table: "UpdateJobs",
            columns: new[] { "PackageEcosystem", "Directory" });

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory_EventBusId",
            table: "UpdateJobs",
            columns: new[] { "PackageEcosystem", "Directory", "EventBusId" },
            unique: true,
            filter: "[EventBusId] IS NOT NULL");
    }
}
