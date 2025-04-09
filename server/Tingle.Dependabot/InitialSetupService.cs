using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;
using Tingle.Extensions.Primitives;

namespace Tingle.Dependabot;

/// <summary>
/// Service to setup projects and repositories on startup based on the configuration.
/// This must be registered as a hosted service after the hosted services responsible for database migrations/creation.
/// </summary>
/// <param name="serviceScopeFactory"></param>
/// <param name="optionsAccessor"></param>
/// <param name="jsonOptionsAccessor"></param>
/// <param name="logger"></param>
internal class InitialSetupService(IServiceScopeFactory serviceScopeFactory,
                                   IOptions<InitialSetupOptions> optionsAccessor,
                                   IOptions<JsonOptions> jsonOptionsAccessor,
                                   ILogger<InitialSetupService> logger) : IHostedService
{
    internal class ProjectSetupInfo
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

    private readonly InitialSetupOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
    private readonly JsonOptions jsonOptions = jsonOptionsAccessor?.Value ?? throw new ArgumentNullException(nameof(jsonOptionsAccessor));

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scope = serviceScopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        // parse projects to be setup
        var setupsJson = options.Projects;
        var setups = new List<ProjectSetupInfo>();
        if (!string.IsNullOrWhiteSpace(options.Projects))
        {
            setups = JsonSerializer.Deserialize<List<ProjectSetupInfo>>(options.Projects, jsonOptions.SerializerOptions)!;
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
                    Password = Keygen.Create(32, Keygen.OutputFormat.Base62), // base62 so that it can be used in the URL if needed
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
                logger.LogInformation("Project {Url} updated", url);
            }
        }

        // update database
        var updated = await context.SaveChangesAsync(cancellationToken);
        if (updated > 0)
        {
            // update database and list of projects
            projects = await context.Projects.ToListAsync(cancellationToken);

            // synchronize and create/update subscriptions if we have setups
            var synchronizer = provider.GetRequiredService<Synchronizer>();
            foreach (var project in projects)
            {
                // synchronize project
                await synchronizer.SynchronizeAsync(project, false, cancellationToken); /* database sync should not trigger, just in case it's too many */

                // create or update webhooks/subscriptions
                await adoProvider.CreateOrUpdateSubscriptionsAsync(project, cancellationToken);
            }
        }

        // skip loading schedules if told to
        if (!options.SkipLoadSchedules)
        {
            var repositories = await context.Repositories.ToListAsync(cancellationToken);
            var scheduler = provider.GetRequiredService<UpdateScheduler>();
            foreach (var repository in repositories)
            {
                await scheduler.CreateOrUpdateAsync(repository, cancellationToken);
            }
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal class InitialSetupOptions
{
    /// <summary>
    /// The JSON array as a string for the projects to be setup.
    /// If provided, it must be a valid JSON and is deserialized into an array
    /// of <see cref="InitialSetupService.ProjectSetupInfo"/>.
    /// </summary>
    /// <example>
    /// [{\"url\":\"https://dev.azure.com/tingle/dependabot\",\"token\":\"dummy\",\"AutoComplete\":true}]
    /// </example>
    public string? Projects { get; set; }

    /// <summary>
    /// Indicates whether the loading of schedules into memory should be skipped.
    /// Each repository has updates and each update has a schedule.
    /// </summary>
    public bool SkipLoadSchedules { get; set; }
}

internal class InitialSetupConfigureOptions : IValidateOptions<InitialSetupOptions>
{
    public ValidateOptionsResult Validate(string? name, InitialSetupOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Projects))
        {
            try
            {
                var node = System.Text.Json.Nodes.JsonNode.Parse(options.Projects);
                node!.AsArray();
            }
            catch (Exception ex)
            {
                return ValidateOptionsResult.Fail(ex.Message);
            }
        }

        return ValidateOptionsResult.Success;
    }
}
