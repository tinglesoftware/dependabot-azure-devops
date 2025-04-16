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
            name: "Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                FriendlyName = table.Column<string>(type: "TEXT", nullable: true),
                Xml = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Created = table.Column<long>(type: "INTEGER", nullable: false),
                Updated = table.Column<long>(type: "INTEGER", nullable: false),
                Type = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Slug = table.Column<string>(type: "TEXT", nullable: true),
                ProviderId = table.Column<string>(type: "TEXT", nullable: false),
                Url = table.Column<string>(type: "TEXT", nullable: false),
                Token = table.Column<string>(type: "TEXT", nullable: false),
                UserId = table.Column<string>(type: "TEXT", nullable: false),
                Private = table.Column<bool>(type: "INTEGER", nullable: false),
                AutoComplete_Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                AutoComplete_IgnoreConfigs = table.Column<string>(type: "TEXT", nullable: true),
                AutoComplete_MergeStrategy = table.Column<int>(type: "INTEGER", nullable: true),
                AutoApprove_Enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                Password = table.Column<string>(type: "TEXT", nullable: false),
                Secrets = table.Column<string>(type: "TEXT", nullable: false),
                Experiments = table.Column<string>(type: "TEXT", nullable: true),
                GithubToken = table.Column<string>(type: "TEXT", nullable: true),
                Debug = table.Column<bool>(type: "INTEGER", nullable: false),
                Synchronized = table.Column<long>(type: "INTEGER", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UpdateJobs",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Created = table.Column<long>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                Trigger = table.Column<int>(type: "INTEGER", nullable: false),
                ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                RepositoryId = table.Column<string>(type: "TEXT", nullable: false),
                RepositorySlug = table.Column<string>(type: "TEXT", nullable: false),
                EventBusId = table.Column<string>(type: "TEXT", nullable: true),
                Commit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                PackageEcosystem = table.Column<string>(type: "TEXT", nullable: false),
                PackageManager = table.Column<string>(type: "TEXT", nullable: false),
                Directory = table.Column<string>(type: "TEXT", nullable: true),
                Directories = table.Column<string>(type: "TEXT", nullable: true),
                Resources_Cpu = table.Column<double>(type: "REAL", nullable: false),
                Resources_Memory = table.Column<double>(type: "REAL", nullable: false),
                ProxyImage = table.Column<string>(type: "TEXT", nullable: true),
                UpdaterImage = table.Column<string>(type: "TEXT", nullable: true),
                AuthKey = table.Column<string>(type: "TEXT", nullable: false),
                Start = table.Column<long>(type: "INTEGER", nullable: true),
                End = table.Column<long>(type: "INTEGER", nullable: true),
                Duration = table.Column<long>(type: "INTEGER", nullable: true),
                LogsPath = table.Column<string>(type: "TEXT", nullable: true),
                FlameGraphPath = table.Column<string>(type: "TEXT", nullable: true),
                Errors = table.Column<string>(type: "TEXT", nullable: false),
                UnknownErrors = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UpdateJobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Repositories",
            columns: table => new
            {
                Id = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                Created = table.Column<long>(type: "INTEGER", nullable: false),
                Updated = table.Column<long>(type: "INTEGER", nullable: false),
                ProjectId = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: true),
                Slug = table.Column<string>(type: "TEXT", nullable: true),
                ProviderId = table.Column<string>(type: "TEXT", nullable: false),
                LatestCommit = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                ConfigFileContents = table.Column<string>(type: "TEXT", nullable: false),
                SyncException = table.Column<string>(type: "TEXT", nullable: true),
                Updates = table.Column<string>(type: "TEXT", nullable: false),
                Registries = table.Column<string>(type: "TEXT", nullable: false)
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
            descending: new bool[0]);

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
            descending: new bool[0]);

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
            descending: new bool[0]);

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory_Directories",
            table: "UpdateJobs",
            columns: new[] { "PackageEcosystem", "Directory", "Directories" });

        migrationBuilder.CreateIndex(
            name: "IX_UpdateJobs_PackageEcosystem_Directory_Directories_EventBusId",
            table: "UpdateJobs",
            columns: new[] { "PackageEcosystem", "Directory", "Directories", "EventBusId" },
            unique: true);

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
            name: "Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys");

        migrationBuilder.DropTable(
            name: "Repositories");

        migrationBuilder.DropTable(
            name: "UpdateJobs");

        migrationBuilder.DropTable(
            name: "Projects");
    }
}
