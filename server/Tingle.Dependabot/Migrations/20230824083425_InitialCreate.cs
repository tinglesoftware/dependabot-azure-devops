using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tingle.Dependabot.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DataProtectionKeys",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Type = table.Column<int>(type: "int", nullable: false),
                Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Slug = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ProviderId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Token = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Private = table.Column<bool>(type: "bit", nullable: false),
                AutoComplete_Enabled = table.Column<bool>(type: "bit", nullable: false),
                AutoComplete_IgnoreConfigs = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AutoComplete_MergeStrategy = table.Column<int>(type: "int", nullable: true),
                AutoApprove_Enabled = table.Column<bool>(type: "bit", nullable: false),
                Password = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Secrets = table.Column<string>(type: "nvarchar(max)", nullable: false),
                UpdaterImageTag = table.Column<string>(type: "nvarchar(max)", nullable: true),
                GithubToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Location = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Synchronized = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Etag = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UpdateJobs",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                Trigger = table.Column<int>(type: "int", nullable: false),
                ProjectId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                RepositoryId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                RepositorySlug = table.Column<string>(type: "nvarchar(max)", nullable: false),
                EventBusId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                Commit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                PackageEcosystem = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Directory = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Resources_Cpu = table.Column<double>(type: "float", nullable: false),
                Resources_Memory = table.Column<double>(type: "float", nullable: false),
                UpdaterImage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                AuthKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Start = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                End = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                Duration = table.Column<long>(type: "bigint", nullable: true),
                Log = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Error_Type = table.Column<string>(type: "nvarchar(450)", nullable: true),
                Error_Detail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Etag = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UpdateJobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Repositories",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                Updated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                ProjectId = table.Column<string>(type: "nvarchar(50)", nullable: false),
                Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Slug = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ProviderId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                LatestCommit = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ConfigFileContents = table.Column<string>(type: "nvarchar(max)", nullable: false),
                SyncException = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Updates = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Registries = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Etag = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Repositories", x => x.Id);
                table.ForeignKey(
                    name: "FK_Repositories_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Projects_Created",
            table: "Projects",
            column: "Created",
            descending: []);

        migrationBuilder.CreateIndex(
            name: "IX_Projects_Password",
            table: "Projects",
            column: "Password",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Projects_ProviderId",
            table: "Projects",
            column: "ProviderId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Repositories_Created",
            table: "Repositories",
            column: "Created",
            descending: []);

        migrationBuilder.CreateIndex(
            name: "IX_Repositories_ProjectId",
            table: "Repositories",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_Repositories_ProviderId",
            table: "Repositories",
            column: "ProviderId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_AuthKey",
            table: "UpdateJobs",
            column: "AuthKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_Created",
            table: "UpdateJobs",
            column: "Created",
            descending: []);

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_Error_Type",
            table: "UpdateJobs",
            column: "Error_Type");

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory",
            table: "UpdateJobs",
            columns: ["PackageEcosystem", "Directory"]);

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory_EventBusId",
            table: "UpdateJobs",
            columns: ["PackageEcosystem", "Directory", "EventBusId"],
            unique: true,
            filter: "[EventBusId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_ProjectId",
            table: "UpdateJobs",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_RepositoryId",
            table: "UpdateJobs",
            column: "RepositoryId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DataProtectionKeys");

        migrationBuilder.DropTable(
            name: "Repositories");

        migrationBuilder.DropTable(
            name: "UpdateJobs");

        migrationBuilder.DropTable(
            name: "Projects");
    }
}
