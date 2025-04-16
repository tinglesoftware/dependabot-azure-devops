using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Workflow;
using Tingle.Extensions.Primitives;
using SC = Tingle.Dependabot.DependabotSerializerContext;

namespace Tingle.Dependabot;

/// <summary>
/// Service to setup projects and repositories on startup based on the configuration.
/// This must be registered as a hosted service after the hosted services responsible for database migrations/creation.
/// </summary>
/// <param name="serviceScopeFactory"></param>
/// <param name="optionsAccessor"></param>
/// <param name="logger"></param>
internal class InitialSetupService(IServiceScopeFactory serviceScopeFactory,
                                   IOptions<InitialSetupOptions> optionsAccessor,
                                   ILogger<InitialSetupService> logger) : IHostedService
{
    internal class ProjectSetupInfo
    {
        public required AzureDevOpsProjectUrl Url { get; set; }
        public required string Token { get; set; }
        public bool AutoComplete { get; set; }
        public List<int>? AutoCompleteIgnoreConfigs { get; set; }
        public Models.Management.MergeStrategy? AutoCompleteMergeStrategy { get; set; }
        public bool AutoApprove { get; set; }
        public string? GithubToken { get; set; }
        public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly InitialSetupOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));

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
            setups = JsonSerializer.Deserialize(options.Projects, SC.Default.ListProjectSetupInfo)!;
        }

        logger.LogInformation("Found {Count} projects to setup", setups.Count);

        // add projects if there are projects to be added
        var adoProvider = provider.GetRequiredService<IAzureDevOpsProvider>();
        var context = provider.GetRequiredService<MainDbContext>();
        var projects = await context.Projects.ToListAsync(cancellationToken);
        foreach (var setup in setups)
        {
            // pull the project from the provider
            var url = setup.Url;
            var token = setup.Token;
            var azdoProject = await adoProvider.GetProjectAsync(url, token, cancellationToken);
            var azdoUser = await adoProvider.GetConnectionDataAsync(url, token, cancellationToken);

            logger.LogInformation("Setting up project: {Url}", url);
            var project = projects.SingleOrDefault(p => p.Url == url);
            if (project is null)
            {
                project = new Models.Management.Project
                {
                    Id = $"prj_{Ksuid.Generate()}",
                    Created = DateTimeOffset.UtcNow,
                    Password = Keygen.Create(32, Keygen.OutputFormat.Base62), // base62 so that it can be used in the URL if needed
                    Url = url,
                    Type = Models.Management.ProjectType.Azure,

                    Name = azdoProject.Name,
                    Description = azdoProject.Description,
                    ProviderId = azdoProject.Id,
                    Slug = url.Slug,
                    Private = azdoProject.Visibility is not Models.Azure.AzdoProjectVisibility.Public,
                    Token = token,
                    UserId = azdoUser.AuthenticatedUser.Id,
                    AutoApprove = new Models.Management.ProjectAutoApprove { Enabled = setup.AutoApprove, },
                    AutoComplete = new Models.Management.ProjectAutoComplete
                    {
                        Enabled = setup.AutoComplete,
                        IgnoreConfigs = setup.AutoCompleteIgnoreConfigs,
                        MergeStrategy = setup.AutoCompleteMergeStrategy,
                    },
                    Secrets = setup.Secrets,
                    GithubToken = setup.GithubToken,
                };
                logger.LogInformation("Adding new project to database: {Url}", url);
                await context.Projects.AddAsync(project, cancellationToken);
            }
            else
            {
                // update project using values from the setup
                project.Name = azdoProject.Name;
                project.Description = azdoProject.Description;
                project.ProviderId = azdoProject.Id;
                project.Slug = url.Slug;
                project.Private = azdoProject.Visibility is not Models.Azure.AzdoProjectVisibility.Public;
                project.Token = token;
                project.UserId = azdoUser.AuthenticatedUser.Id;
                project.AutoComplete = new Models.Management.ProjectAutoComplete
                {
                    Enabled = setup.AutoComplete,
                    IgnoreConfigs = setup.AutoCompleteIgnoreConfigs,
                    MergeStrategy = setup.AutoCompleteMergeStrategy,
                };
                project.AutoApprove = new Models.Management.ProjectAutoApprove { Enabled = setup.AutoApprove, };
                project.Secrets = setup.Secrets;
                project.GithubToken = setup.GithubToken;

                // if there are changes, set the Updated field
                if (context.Entry(project).State is EntityState.Modified)
                {
                    project.Updated = DateTimeOffset.UtcNow;
                    logger.LogInformation("Project {Url} updated", url);
                }
            }
        }

        // update database
        var updated = await context.SaveChangesAsync(cancellationToken);
        if (updated > 0)
        {
            // update database and list of projects
            projects = await context.Projects.ToListAsync(cancellationToken);

            // synchronize and create/update subscriptions if we have setups
            var synchronizer = provider.GetRequiredService<ISynchronizer>();
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
            var scheduler = provider.GetRequiredService<IUpdateScheduler>();
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
