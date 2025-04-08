using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;
using Tingle.Extensions.Primitives;

namespace Tingle.Dependabot;

internal static class AppSetup
{
    private class ProjectSetupInfo
    {
        public required AzureDevOpsProjectUrl Url { get; set; }
        public required string Token { get; set; }
        public string? UpdaterImageTag { get; set; }
        public bool AutoComplete { get; set; }
        public List<int>? AutoCompleteIgnoreConfigs { get; set; }
        public MergeStrategy? AutoCompleteMergeStrategy { get; set; }
        public bool AutoApprove { get; set; }
        public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task SetupAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AppSetup).FullName!);

        // perform migrations on startup if asked to
        if (app.Configuration.GetValue<bool>("EFCORE_PERFORM_MIGRATIONS"))
        {
            var db = provider.GetRequiredService<MainDbContext>().Database;
            if (db.IsRelational()) // only relational databases
            {
                logger.LogInformation("Performing EF Core migrations on startup");
                await db.MigrateAsync(cancellationToken: cancellationToken);
            }
        }

        // parse projects to be setup
        var setupsJson = app.Configuration.GetValue<string?>("PROJECT_SETUPS");
        var setups = new List<ProjectSetupInfo>();
        if (!string.IsNullOrWhiteSpace(setupsJson))
        {
            setups = JsonSerializer.Deserialize<List<ProjectSetupInfo>>(setupsJson, serializerOptions)!;
        }

        logger.LogInformation("Found {Count} projects to setup", setups.Count);

        // add projects if there are projects to be added
        var adoProvider = provider.GetRequiredService<AzureDevOpsProvider>();
        var context = provider.GetRequiredService<MainDbContext>();
        var projects = await context.Projects.ToListAsync(cancellationToken);
        foreach (var setup in setups)
        {
            var url = setup.Url;
            logger.LogInformation("Setting up project: {Url}", url);
            var project = projects.SingleOrDefault(p => p.Url == setup.Url);
            if (project is null)
            {
                project = new Models.Management.Project
                {
                    Id = $"prj_{Ksuid.Generate()}",
                    Created = DateTimeOffset.UtcNow,
                    Password = GeneratePassword(32),
                    Url = setup.Url.ToString(),
                    Type = Models.Management.ProjectType.Azure,
                };
                logger.LogInformation("Adding new project to database: {Url}", url);
                await context.Projects.AddAsync(project, cancellationToken);
            }

            // update project using values from the setup
            project.Token = setup.Token;
            project.UpdaterImageTag = setup.UpdaterImageTag;
            project.AutoComplete.Enabled = setup.AutoComplete;
            project.AutoComplete.IgnoreConfigs = setup.AutoCompleteIgnoreConfigs;
            project.AutoComplete.MergeStrategy = setup.AutoCompleteMergeStrategy;
            project.AutoApprove.Enabled = setup.AutoApprove;
            project.Secrets = setup.Secrets;

            // update values from the project
            var tp = await adoProvider.GetProjectAsync(project, cancellationToken);
            project.ProviderId = tp.Id.ToString();
            project.Name = tp.Name;
            project.Description = tp.Description;
            project.Slug = url.Slug;
            project.Private = tp.Visibility is not Models.Azure.AzdoProjectVisibility.Public;

            // if there are changes, set the Updated field
            if (context.ChangeTracker.HasChanges())
            {
                project.Updated = DateTimeOffset.UtcNow;
            }
        }

        // update database and list of projects
        var updated = await context.SaveChangesAsync(cancellationToken);
        projects = updated > 0 ? await context.Projects.ToListAsync(cancellationToken) : projects;

        // synchronize and create/update subscriptions if we have setups
        var synchronizer = provider.GetRequiredService<Synchronizer>();
        if (setups.Count > 0)
        {
            foreach (var project in projects)
            {
                // synchronize project
                await synchronizer.SynchronizeAsync(project, false, cancellationToken); /* database sync should not trigger, just in case it's too many */

                // create or update webhooks/subscriptions
                await adoProvider.CreateOrUpdateSubscriptionsAsync(project, cancellationToken);
            }
        }

        // skip loading schedules if told to
        if (!app.Configuration.GetValue<bool>("SKIP_LOAD_SCHEDULES"))
        {
            var repositories = await context.Repositories.ToListAsync(cancellationToken);
            var scheduler = provider.GetRequiredService<UpdateScheduler>();
            foreach (var repository in repositories)
            {
                await scheduler.CreateOrUpdateAsync(repository, cancellationToken);
            }
        }
    }

    private static string GeneratePassword(int length = 32)
    {
        var data = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(data);
        return Convert.ToBase64String(data);
    }
}
