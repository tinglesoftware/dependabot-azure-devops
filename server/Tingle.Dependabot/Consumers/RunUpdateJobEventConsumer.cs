using Microsoft.EntityFrameworkCore;
using Tingle.Dependabot.Events;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.Dependabot.Workflow;
using Tingle.EventBus;
using Tingle.Extensions.Primitives;

namespace Tingle.Dependabot.Consumers;

internal class RunUpdateJobEventConsumer(MainDbContext dbContext,
                                         IUpdateRunner runner,
                                         IScenarioStore scenarioStore,
                                         IAzureDevOpsProvider adoProvider,
                                         GitHubGraphClient gitHubGraphClient,
                                         ILogger<RunUpdateJobEventConsumer> logger) : IEventConsumer<RunUpdateJobEvent>
{
    public async Task ConsumeAsync(EventContext<RunUpdateJobEvent> context, CancellationToken cancellationToken)
    {
        var evt = context.Event;

        // ensure project exists
        var projectId = evt.ProjectId ?? throw new InvalidOperationException($"'{nameof(evt.ProjectId)}' cannot be null");
        var project = await dbContext.Projects.SingleOrDefaultAsync(r => r.Id == projectId, cancellationToken);
        if (project is null)
        {
            logger.SkippingTriggerProjectNotFound(projectId);
            return;
        }

        // ensure repository exists
        var repositoryId = evt.RepositoryId ?? throw new InvalidOperationException($"'{nameof(evt.RepositoryId)}' cannot be null");
        var repository = await dbContext.Repositories.SingleOrDefaultAsync(r => r.Id == repositoryId, cancellationToken);
        if (repository is null)
        {
            logger.SkippingTriggerRepositoryNotFound(repositoryId: repositoryId, projectId: project.Id);
            return;
        }

        // if we have a specific update to trigger, use it otherwise, do all
        var repositoryUpdateId = evt.RepositoryUpdateId;
        var update = repository.Updates.ElementAtOrDefault(repositoryUpdateId);
        if (update is null)
        {
            logger.SkippingTriggerRepositoryUpdateNotFound(repositoryId: repositoryId, repositoryUpdateId, projectId: project.Id);
            return;
        }

        // trigger each update
        var eventBusId = context.Id;
        var ecosystem = update.PackageEcosystem;

        // check if there is an existing one
        var job = await (from j in dbContext.UpdateJobs
                         where j.PackageEcosystem == ecosystem
                         where j.Directory == update.Directory
                         where j.Directories == update.Directories
                         where j.EventBusId == eventBusId
                         select j).SingleOrDefaultAsync(cancellationToken);
        if (job is not null)
        {
            logger.SkippingTriggerJobAlreadyExists(repositoryId: repository.Id,
                                                    repositoryUpdateId: repository.Updates.IndexOf(update),
                                                    projectId: project.Id,
                                                    eventBusId: eventBusId);
            return;
        }

        // create the job
        var resources = UpdateJobResources.FromEcosystem(ecosystem); // decide the resources based on the ecosystem
        var jobId = $"{SequenceNumber.Generate()}";
        job = new UpdateJob
        {
            Id = jobId,
            Created = DateTimeOffset.UtcNow,
            Status = UpdateJobStatus.Running,
            Trigger = evt.Trigger,

            ProjectId = project.Id,
            RepositoryId = repository.Id,
            RepositorySlug = repository.Slug,
            EventBusId = eventBusId,

            Commit = repository.LatestCommit,
            PackageEcosystem = ecosystem,
            PackageManager = ConvertEcosystemToPackageManager(ecosystem),
            Directory = update.Directory,
            Directories = update.Directories,
            Resources = resources,
            AuthKey = Keygen.Create(32, Keygen.OutputFormat.Base62),

            ProxyImage = null,
            UpdaterImage = null,
            LogsPath = null,
            FlameGraphPath = null,

            Start = null,
            End = null,
            Duration = null,
        };
        await dbContext.UpdateJobs.AddAsync(job, cancellationToken);

        // update the RepositoryUpdate
        update.LatestJobId = job.Id;
        update.LatestJobStatus = job.Status;
        update.LatestUpdate = job.Created;

        // save to the database
        await dbContext.SaveChangesAsync(cancellationToken);

        // fetch existing pull requests
        Dictionary<string, Models.Azure.PullRequestProperties> existingPullRequestsMapped = [];
        var existingPullRequests = await adoProvider.GetActivePullRequestsAsync(project, repository.ProviderId, cancellationToken);
        foreach (var pr in existingPullRequests)
        {
            var props = await adoProvider.GetPullRequestPropertiesAsync(project, repository.ProviderId, pr.PullRequestId, cancellationToken);
            if (props is null) continue;
            existingPullRequestsMapped[$"{pr.PullRequestId}"] = new Models.Azure.PullRequestProperties(
                PackageManager: props.PackageManager,
                Dependencies: props.Dependencies
            );
        }

        List<string> dependencyNamesToUpdate = [];
        List<Models.Dependabot.DependabotSecurityAdvisory>? securityAdvisories = [];

        if (update.SecurityOnly)
        {
            // TODO; we need to ensure we have the dependencies prior to security only updates

            List<Models.GitHub.GitHubPackage> packages = [];

            var ghsaEcosystem = ConvertDependabotPackageManagerToGhsaEcosystem(job.PackageManager);
            var vulnerabilities = await gitHubGraphClient.GetSecurityVulnerabilitiesAsync(project.Token, ghsaEcosystem, packages, cancellationToken);
            foreach (var vulnerability in vulnerabilities)
            {
                var vulnerableVersionRange = vulnerability.VulnerableVersionRange;
                var firstPatchedVersion = vulnerability.FirstPatchedVersion;
                securityAdvisories.Add(
                    new Models.Dependabot.DependabotSecurityAdvisory(
                        DependencyName: vulnerability.Package.Name,
                        AffectedVersions: string.IsNullOrEmpty(vulnerableVersionRange) ? [] : [vulnerableVersionRange],
                        PatchedVersions: string.IsNullOrEmpty(firstPatchedVersion) ? [] : [firstPatchedVersion],
                        UnaffectedVersions: []
                    ));
            }

            // Only update dependencies that have vulnerabilities
            dependencyNamesToUpdate = [.. vulnerabilities.Select(v => v.Package.Name)];
        }

        // call the update runner to run the update
        var updaterContext = new UpdaterContext
        {
            Project = project,
            Repository = repository,
            Update = update,
            Job = job,

            ExistingPullRequests = existingPullRequestsMapped,
            SecurityAdvisories = securityAdvisories,

            UpdatingPullRequest = false, // TODO: fix this
            DependencyGroupToRefresh = evt.DependencyGroupToRefresh,
            DependencyNamesToUpdate = dependencyNamesToUpdate,
        };
        await runner.RunAsync(updaterContext, cancellationToken);

        // save changes made by the runner
        await dbContext.SaveChangesAsync(cancellationToken);

        // call the scenario store to apply
        var defaultBranch = await adoProvider.GetDefaultBranchAsync(project, repository.ProviderId, cancellationToken);
        var scenarioContext = new ScenarioApplicationContext(dbContext.SaveChangesAsync)
        {
            Project = project,
            Repository = repository,
            Update = update,
            Job = job,

            AdoProvider = adoProvider,
            DefaultBranch = defaultBranch,
            ExistingPullRequests = existingPullRequestsMapped,
        };
        await scenarioStore.ApplyAsync(scenarioContext, cancellationToken);

        // save changes made by the scenario store
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static string ConvertEcosystemToPackageManager(string ecosystem)
    {
        ArgumentException.ThrowIfNullOrEmpty(ecosystem);

        return ecosystem switch
        {
            "dotnet-sdk" => "dotnet_sdk",
            "github-actions" => "github_actions",
            "gitsubmodule" => "submodules",
            "gomod" => "go_modules",
            "mix" => "hex",
            "npm" => "npm_and_yarn",
            // Additional ones
            "yarn" => "npm_and_yarn",
            "pnpm" => "npm_and_yarn",
            "pipenv" => "pip",
            "pip-compile" => "pip",
            "poetry" => "pip",
            _ => ecosystem,
        };
    }
    internal static string ConvertDependabotPackageManagerToGhsaEcosystem(string packageManager)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageManager);

        return packageManager switch
        {
            "compose" => "COMPOSER",
            "elm" => "ERLANG",
            "github_actions" => "ACTIONS",
            "go_modules" => "GO",
            "maven" => "MAVEN",
            "npm_and_yarn" => "NPM",
            "nuget" => "NUGET",
            "pip" => "PIP",
            "pub" => "PUB",
            "bundler" => "RUBYGEMS",
            "cargo" => "RUST",
            "swift" => "SWIFT",
            _ => throw new InvalidOperationException($"Unknown dependabot package manager: {packageManager}"),
        };
    }

    private record Entities(Project Project, Repository Repository, UpdateJob Job, RepositoryUpdate? Update);
    private async Task<Entities> GetEntitiesAsync(string id, CancellationToken cancellationToken = default)
    {
        var job = await dbContext.UpdateJobs.SingleAsync(j => j.Id == id, cancellationToken);
        var repository = await dbContext.Repositories.SingleAsync(r => r.Id == job.RepositoryId, cancellationToken);
        var project = await dbContext.Projects.SingleAsync(p => p.Id == repository.ProjectId, cancellationToken);
        var update = repository.GetUpdate(job);

        return new Entities(project, repository, job, update);
    }
}
