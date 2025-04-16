using System.Text.Json.Nodes;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;

namespace Tingle.Dependabot.Workflow;

public interface IScenarioStore
{
    /// <summary>Add an operation in the singleton store for applying later.</summary>
    /// <param name="id">Identifier of the update job.</param>
    /// <param name="type">Type of operation.</param>
    /// <param name="input">The request input received for the operation.</param>
    /// <param name="cancellationToken"></param>
    Task AddAsync(string id, DependabotOperationType type, object input, CancellationToken cancellationToken = default);

    /// <summary>Apply operations for a given update job.</summary>
    /// <param name="context">Context containing all the objects necessary for applying.</param>
    /// <param name="cancellationToken"></param>
    Task ApplyAsync(ScenarioApplicationContext context, CancellationToken cancellationToken = default);
}

// We store scenarios so that we can apply then all at once at the end.
// Otherwise, if we did apply them on every HTTP request, the database may not be coherent, especially with Sqlite
// It also prevents us from storing pull requests in the database or querying azure devops on every request
internal class ScenarioStore(ILogger<ScenarioStore> logger) : IScenarioStore
{
    private readonly DependabotConcurrentDictionary<string, (SemaphoreSlim, List<ScenarioOutput>)> store = new();

    public async Task AddAsync(string id, DependabotOperationType type, object input, CancellationToken cancellationToken = default)
    {
        static Task<(SemaphoreSlim, List<ScenarioOutput>)> StoreItemCreator(string id, CancellationToken ct)
            => Task.FromResult((new SemaphoreSlim(1), new List<ScenarioOutput>()));

        var (limiter, outputs) = await store.GetOrAddAsync(id, StoreItemCreator, cancellationToken);
        await limiter.WaitAsync(cancellationToken);
        outputs.Add(new(type, input));
        limiter.Release();
    }

    public async Task ApplyAsync(ScenarioApplicationContext context, CancellationToken cancellationToken = default)
    {
        var job = context.Job;
        if (!store.Remove(job.Id, out var item)) throw new InvalidOperationException($"Unable to find scenarios for '{job.Id}'");
        var (limiter, scenarios) = await item.WaitAsync(cancellationToken);
        limiter.Dispose();

        logger.LogInformation("Applying {ScenariosCount} scenarios for {UpdateJobId}", scenarios.Count, job.Id);

        foreach (var scenario in scenarios)
        {
            var (type, input) = scenario;
            logger.LogInformation("Applying scenario {ScenarioNumber}/{ScenarioCount} ({ScenarioType}) for {UpdateJobId}",
                                  scenarios.IndexOf(scenario),
                                  scenarios.Count,
                                  type,
                                  job.Id);

            try
            {
                var handler = type switch
                {
                    DependabotOperationType.RecordUpdateJobError => ApplyAsync(context, (DependabotRequest<DependabotRecordUpdateJobError>)input, cancellationToken),
                    DependabotOperationType.RecordUpdateJobUnknownError => ApplyAsync(context, (DependabotRequest<DependabotRecordUpdateJobUnknownError>)input, cancellationToken),
                    DependabotOperationType.UpdateDependencyList => ApplyAsync(context, (DependabotRequest<DependabotUpdateDependencyList>)input, cancellationToken),
                    DependabotOperationType.CreatePullRequest => ApplyAsync(context, (DependabotRequest<DependabotCreatePullRequest>)input, cancellationToken),
                    DependabotOperationType.UpdatePullRequest => ApplyAsync(context, (DependabotRequest<DependabotUpdatePullRequest>)input, cancellationToken),
                    DependabotOperationType.ClosePullRequest => ApplyAsync(context, (DependabotRequest<DependabotClosePullRequest>)input, cancellationToken),
                    DependabotOperationType.MarkAsProcessed => Task.CompletedTask, // nothing to do here
                    DependabotOperationType.IncrementMetric => Task.CompletedTask, // nothing to do here
                    DependabotOperationType.RecordEcosystemVersions => Task.CompletedTask, // nothing to do here
                    _ => throw new InvalidOperationException($"'{type}' scenario has not been handled"),
                };

                await handler.WaitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                                "Failed applying scenario {ScenarioNumber}/{ScenarioCount} ({ScenarioType}) for {UpdateJobId}",
                                scenarios.IndexOf(scenario),
                                scenarios.Count,
                                type,
                                job.Id);
            }
            finally
            {
                await context.CheckpointAsync(cancellationToken);
            }
        }
    }

    internal static Task ApplyAsync(ScenarioApplicationContext context, DependabotRequest<DependabotRecordUpdateJobError> request, CancellationToken cancellationToken = default)
    {
        context.Job.Errors.Add(new UpdateJobError
        {
            Type = request.Data!.ErrorType,
            Detail = request.Data.ErrorDetails,
        });
        return Task.CompletedTask;
    }
    internal static Task ApplyAsync(ScenarioApplicationContext context, DependabotRequest<DependabotRecordUpdateJobUnknownError> request, CancellationToken cancellationToken = default)
    {
        context.Job.UnknownErrors.Add(new UpdateJobError
        {
            Type = request.Data!.ErrorType,
            Detail = request.Data.ErrorDetails,
        });
        return Task.CompletedTask;
    }
    internal static Task ApplyAsync(ScenarioApplicationContext context, DependabotRequest<DependabotUpdateDependencyList> request, CancellationToken cancellationToken = default)
    {
        context.Update.Files = request.Data.DependencyFiles ?? [];
        return Task.CompletedTask;
    }
    internal static async Task ApplyAsync(ScenarioApplicationContext context, DependabotRequest<DependabotCreatePullRequest> request, CancellationToken cancellationToken = default)
    {
        var job = context.Job;
        var update = context.Update;
        var data = request.Data;

        var openPullRequestsLimit = update.OpenPullRequestsLimit;
        var openPullRequestsCount = context.CreatedPullRequestIds.Count + context.ExistingPullRequests.Count;
        if (openPullRequestsLimit > 0 && openPullRequestsCount >= openPullRequestsLimit) return;

        var changedFiles = GetPullRequestChangedFilesForOutputData(data.DependencyFiles);
        var dependencies = GetPullRequestDependenciesPropertyValueForOutputData(data.Dependencies, data.DependencyGroup);
        var targetBranch = update.TargetBranch ?? context.DefaultBranch ?? throw new InvalidOperationException("Cannot determine target branch");

        var sourceBranch = BranchNameHelper.GetBranchNameForUpdate(
            packageEcosystem: job.PackageEcosystem,
            targetBranchName: targetBranch,
            directory: (update.Directory ?? update.Directories?.FirstOrDefault((dir) => changedFiles[0].Path.StartsWith(dir)))!,
            dependencyGroupName: dependencies.DependencyGroupName,
            dependencies: dependencies.Dependencies,
            separator: update.PullRequestBranchName?.Separator
        );

        // TODO: implement this
        await Task.Yield();
    }
    internal static async Task ApplyAsync(ScenarioApplicationContext context, DependabotRequest<DependabotUpdatePullRequest> request, CancellationToken cancellationToken = default)
    {
        // TODO: implement this
        await Task.Yield();
    }
    internal async Task ApplyAsync(ScenarioApplicationContext context, DependabotRequest<DependabotClosePullRequest> request, CancellationToken cancellationToken = default)
    {
        var data = request.Data;
        var dependencyNames = data.DependencyNames;

        var pullRequestToClose = GetPullRequestForDependencyNames(context, dependencyNames);
        if (pullRequestToClose is null)
        {
            logger.LogError("Could not find pull request to close for package manager '{PackageManager}' with dependencies '{dependencyNames}'",
                            context.Job.PackageManager,
                            string.Join(", ", dependencyNames));
            return;
        }

        // TODO: GitHub Dependabot will close with reason "Superseded by ${new_pull_request_id}" when another PR supersedes it.
        //       How do we detect this? Do we need to?

        // Close the pull request
        var adoProvider = context.AdoProvider;
        var comment = GetPullRequestCloseReasonForOutputData(data);
        await adoProvider.AbandonPullRequestAsync(project: context.Project,
                                                  repositoryIdOrName: context.Repository.ProviderId,
                                                  pullRequestId: pullRequestToClose.Value,
                                                  comment: comment,
                                                  cancellationToken: cancellationToken);

        await Task.Yield();
    }

    internal static List<Models.Azure.FileChange> GetPullRequestChangedFilesForOutputData(List<DependabotDependencyFile> dependencyFiles)
    {
        return [.. dependencyFiles.Where(f =>f.Type == "file").Select(f =>
        {
            var changeType = Models.Azure.VersionControlChangeType.None;
            if (f.Deleted is true) changeType = Models.Azure.VersionControlChangeType.Delete;
            else if (f.Operation is "update") changeType = Models.Azure.VersionControlChangeType.Edit;
            else changeType = Models.Azure.VersionControlChangeType.Add;

            return new Models.Azure.FileChange(
                ChangeType: changeType,
                Path: Path.Join(f.Directory, f.Name),
                Content: f.Content,
                Encoding: f.ContentEncoding
            );
        })];
    }
    internal static Models.Azure.PullRequestStoredDependencies GetPullRequestDependenciesPropertyValueForOutputData(List<DependabotDependency> dependencies, JsonObject? dependencyGroup)
    {
        var deps = dependencies.Select(dep => (Models.Azure.PullRequestStoredDependency)dep).ToList();
        var dependencyGroupName = dependencyGroup?["name"]!.GetValue<string>();

        return new Models.Azure.PullRequestStoredDependencies(
            Dependencies: deps,
            DependencyGroupName: dependencyGroupName);
    }
    internal static int? GetPullRequestForDependencyNames(ScenarioApplicationContext context, List<string> dependencyNames)
    {
        foreach (var (id, stored) in context.ExistingPullRequests)
        {
            var (packageManager, storedDeps) = stored;
            var depNames = storedDeps.Dependencies.Select(dep => dep.Name);
            if (context.Job.PackageManager == packageManager && dependencyNames.SequenceEqual(depNames))
            {
                return int.Parse(id);
            }
        }

        return null;
    }
    internal static string? GetPullRequestCloseReasonForOutputData(DependabotClosePullRequest data)
    {
        // The first dependency is the "lead" dependency in a multi-dependency update
        var leadDependencyName = data.DependencyNames[0];
        var reason = data.Reason switch
        {
            "dependencies_changed" => "Looks like the dependencies have changed",
            "dependency_group_empty" => "Looks like the dependencies in this group are now empty",
            "dependency_removed" => $"Looks like {leadDependencyName} is no longer a dependency",
            "up_to_date" => $"Looks like {leadDependencyName} is up-to-date now",
            "update_no_longer_possible" => $"Looks like {leadDependencyName} can no longer be updated",
            _ => null,
        };

        return reason is not null ? $"{reason}, so this is no longer needed." : reason;
    }
}

public record ScenarioOutput(DependabotOperationType Type, object Expect);

public readonly struct ScenarioApplicationContext
{
    private readonly Func<CancellationToken, Task> checkpoint;

    /// <summary>Creates an instance of <see cref="ScenarioApplicationContext"/></summary>
    /// <param name="checkpoint">A function that is called after every operation to save the current changes.</param>
    public ScenarioApplicationContext(Func<CancellationToken, Task> checkpoint) : this()
    {
        ArgumentNullException.ThrowIfNull(this.checkpoint = checkpoint, nameof(checkpoint));
    }

    /// <summary>The project where the scenarios are to be applied.</summary>
    public required Project Project { get; init; }

    /// <summary>The repository where the scenarios are to be applied.</summary>
    public required Repository Repository { get; init; }

    /// <summary>The repository update for which the scenarios were generated.</summary>
    public required RepositoryUpdate Update { get; init; }

    /// <summary>The update job that generated the scenarios to be applied.</summary>
    public required UpdateJob Job { get; init; }

    /// <summary>
    /// Provider for accessing Azure DevOps API.
    /// <br />
    /// This is not injected because the applier is singleton whereas the caller is scoped.
    /// It also helps to keep within the scope of the creator.
    /// </summary>
    public required IAzureDevOpsProvider AdoProvider { get; init; }

    /// <summary>
    /// Save the changed made at a given point during the application of scenarios.
    /// This should be called after apply each scenario to avoid too many pending changes.
    /// However, it can be called at any point where there is fear of loss or changes.
    /// <br />
    /// This exists because the applier is singleton container whereas the caller is scoped.
    /// It also helps to keep within the scope of the creator.
    /// </summary>
    /// <param name="cancellationToken"></param>
    public Task CheckpointAsync(CancellationToken cancellationToken = default) => checkpoint(cancellationToken);

    public string? DefaultBranch { get; init; }

    public List<string> CreatedPullRequestIds { get; } = [];
    public required IReadOnlyDictionary<string, Models.Azure.PullRequestProperties> ExistingPullRequests { get; init; }
}
