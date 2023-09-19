using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tingle.Dependabot.Models;

namespace Tingle.Dependabot.Workflow;

internal partial class UpdateRunner
{
    [GeneratedRegex("\\${{\\s*([a-zA-Z_]+[a-zA-Z0-9_-]*)\\s*}}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderPattern();

    private const string UpdaterContainerName = "updater";

    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly WorkflowOptions options;
    private readonly ILogger logger;

    private readonly ArmClient armClient;
    private readonly ResourceGroupResource resourceGroup;
    private readonly LogsQueryClient logsQueryClient;

    public UpdateRunner(IOptions<WorkflowOptions> optionsAccessor, ILogger<UpdateRunner> logger)
    {
        options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        armClient = new ArmClient(new DefaultAzureCredential());
        resourceGroup = armClient.GetResourceGroupResource(new(options.ResourceGroupId!));
        logsQueryClient = new LogsQueryClient(new DefaultAzureCredential());
    }

    public async Task CreateAsync(Repository repository, RepositoryUpdate update, UpdateJob job, CancellationToken cancellationToken = default)
    {
        var resourceName = MakeResourceName(job);

        // if we have an existing one, there is nothing more to do
        var containerAppJobs = resourceGroup.GetContainerAppJobs();
        try
        {
            var response = await containerAppJobs.GetAsync(resourceName, cancellationToken);
            if (response.Value is not null) return;
        }
        catch (Azure.RequestFailedException rfe) when (rfe.Status is 404) { }

        // prepare the container
        var volumeName = "working-dir";
        var container = new ContainerAppContainer
        {
            Name = UpdaterContainerName,
            Image = options.UpdaterContainerImageTemplate!.Replace("{{ecosystem}}", job.PackageEcosystem),
            Resources = job.Resources!,
            Args = { "update_script", },
            VolumeMounts = { new ContainerAppVolumeMount { VolumeName = volumeName, MountPath = "/mnt/dependabot", }, },
        };
        var env = CreateVariables(repository, update, job);
        foreach (var (key, value) in env) container.Env.Add(new ContainerAppEnvironmentVariable { Name = key, Value = value, });

        // prepare the ContainerApp job
        var data = new ContainerAppJobData(options.Location!)
        {
            EnvironmentId = options.AppEnvironmentId,
            Configuration = new ContainerAppJobConfiguration(ContainerAppJobTriggerType.Manual, 1)
            {
                ManualTriggerConfig = new JobConfigurationManualTriggerConfig
                {
                    Parallelism = 1,
                    ReplicaCompletionCount = 1,
                },
                ReplicaRetryLimit = 1,
                ReplicaTimeout = Convert.ToInt32(TimeSpan.FromHours(1).TotalSeconds),
            },
            Template = new ContainerAppJobTemplate
            {
                Containers = { container, },
                Volumes =
                {
                    new ContainerAppVolume
                    {
                        Name = volumeName,
                        StorageType = ContainerAppStorageType.AzureFile,
                        StorageName = volumeName,
                    },
                },
            },

            // add tags to the data for tracing purposes
            Tags =
            {
                ["purpose"] = "dependabot",
                ["ecosystem"] = job.PackageEcosystem,
                ["repository"] = repository.Slug,
                ["directory"] = update.Directory,
                ["machine-name"] = Environment.MachineName,
            },
        };

        // create the ContainerApp Job
        var operation = await containerAppJobs.CreateOrUpdateAsync(Azure.WaitUntil.Completed, resourceName, data, cancellationToken);
        logger.LogInformation("Created ContainerApp Job for {UpdateJobId}", job.Id);

        // start the ContainerApp Job
        _ = await operation.Value.StartAsync(Azure.WaitUntil.Completed, cancellationToken: cancellationToken);
        logger.LogInformation("Started ContainerApp Job for {UpdateJobId}", job.Id);
        job.Status = UpdateJobStatus.Running;
    }

    public async Task DeleteAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var resourceName = MakeResourceName(job);

        try
        {
            // if it does not exist, there is nothing more to do
            var containerAppJobs = resourceGroup.GetContainerAppJobs();
            var response = await containerAppJobs.GetAsync(resourceName, cancellationToken);
            if (response.Value is null) return;

            // delete the container group
            await response.Value.DeleteAsync(Azure.WaitUntil.Completed, cancellationToken);
        }
        catch (Azure.RequestFailedException rfe) when (rfe.Status is 404) { }
    }

    public async Task<UpdateRunnerState?> GetStateAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var resourceName = MakeResourceName(job);

        try
        {
            // if it does not exist, there is nothing more to do
            var response = await resourceGroup.GetContainerAppJobAsync(resourceName, cancellationToken);
            var resource = response.Value;

            // if there is no execution, there is nothing more to do
            var executions = await resource.GetContainerAppJobExecutions().GetAllAsync(cancellationToken: cancellationToken).ToListAsync(cancellationToken: cancellationToken);
            var execution = executions.SingleOrDefault();
            if (execution is null) return null;

            // this is a temporary workaround
            // TODO: remove this after https://github.com/Azure/azure-sdk-for-net/issues/38385 is fixed
            var rr = await resource.GetContainerAppJobExecutionAsync(execution.Data.Name, cancellationToken);
            var properties = JsonNode.Parse(rr.GetRawResponse().Content.ToString())!.AsObject()["properties"]!;

            //var status = execution.Data.Properties.Status.ToString() switch
            var status = properties["status"]!.GetValue<string>() switch
            {
                "Succeeded" => UpdateJobStatus.Succeeded,
                "Running" => UpdateJobStatus.Running,
                "Processing" => UpdateJobStatus.Running,
                _ => UpdateJobStatus.Failed,
            };

            // there is no state for jobs that are running
            if (status is UpdateJobStatus.Running) return null;

            // delete the job directory f it exists
            var jobDirectory = Path.Join(options.WorkingDirectory, job.Id);
            if (Directory.Exists(jobDirectory))
            {
                Directory.Delete(jobDirectory, recursive: true);
            }

            // get the period
            //DateTimeOffset? start = execution.Data.Properties.StartTime, end = execution.Data.Properties.EndTime;
            DateTimeOffset? start = properties["startTime"]?.GetValue<DateTimeOffset?>(), end = properties["endTime"]?.GetValue<DateTimeOffset?>();

            // create and return state
            return new UpdateRunnerState(status, start, end);
        }
        catch (Azure.RequestFailedException rfe) when (rfe.Status is 404) { }

        return null;
    }

    public async Task<string?> GetLogsAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var logs = (string?)null;
        var resourceName = MakeResourceName(job);

        // pull logs from Log Analaytics
        if (string.IsNullOrWhiteSpace(logs))
        {
            var query = $"ContainerAppConsoleLogs_CL | where ContainerJobName_s == '{resourceName}' | order by _timestamp_d asc | project Log_s";
            var response = await logsQueryClient.QueryWorkspaceAsync<string>(workspaceId: options.LogAnalyticsWorkspaceId,
                                                                             query: query,
                                                                             timeRange: QueryTimeRange.All,
                                                                             cancellationToken: cancellationToken);

            logs = string.Join(Environment.NewLine, response.Value);
        }

        return logs;
    }

    internal static string MakeResourceName(UpdateJob job) => $"dependabot-{job.Id}";

    internal IDictionary<string, string> CreateVariables(Repository repository, RepositoryUpdate update, UpdateJob job)
    {
        static string? ToJson<T>(T? entries) => entries is null ? null : JsonSerializer.Serialize(entries, serializerOptions); // null ensures we do not add to the values

        var jobDirectory = Path.Join(options.WorkingDirectory, job.Id);

        // TODO: write the job definition file (find out if it is YAML/JSON)

        //    var attr = new UpdateJobAttributes(job)
        //    {
        //        AllowedUpdates = Array.Empty<object>(),
        //        CredentialsMetadata = Array.Empty<object>(),
        //        Dependencies = Array.Empty<object>(),
        //        Directory = job.Directory!,
        //        ExistingPullRequests = Array.Empty<object>(),
        //        IgnoreConditions = Array.Empty<object>(),
        //        PackageManager = job.PackageEcosystem,
        //        RepoName = job.RepositorySlug!,
        //        SecurityAdvisories = Array.Empty<object>(),
        //        Source = new UpdateJobAttributesSource
        //        {
        //            Directory = job.Directory!,
        //            Provider = "azure",
        //            Repo = job.RepositorySlug!,
        //            Branch = job.Branch,
        //            Hostname = ,
        //            ApiEndpoint =,
        //        },
        //    };

        // Add compulsory values
        var values = new Dictionary<string, string>
        {
            ["DEPENDABOT_JOB_ID"] = job.Id!,
            ["DEPENDABOT_JOB_TOKEN"] = job.AuthKey!,
            ["DEPENDABOT_JOB_PATH"] = Path.Join(jobDirectory, "job.json"),
            ["DEPENDABOT_OUTPUT_PATH"] = Path.Join(jobDirectory, "output"),

            ["DEPENDABOT_PACKAGE_MANAGER"] = job.PackageEcosystem!,
            ["DEPENDABOT_DIRECTORY"] = update.Directory!,
            ["DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT"] = update.OpenPullRequestsLimit!.Value.ToString(),
        };

        // Add optional values
        values.AddIfNotDefault("DEPENDABOT_DEBUG", options.DebugJobs?.ToString().ToLower())
              .AddIfNotDefault("DEPENDABOT_API_URL", options.JobsApiUrl)
              // Setting DEPENDABOT_REPO_CONTENTS_PATH causes some issues, ignore till we can resolve
              //.AddIfNotDefault("DEPENDABOT_REPO_CONTENTS_PATH", Path.Join(jobDirectory, "repo"))
              .AddIfNotDefault("UPDATER_DETERMINISTIC", options.DeterministicUpdates?.ToString().ToLower());

        values.AddIfNotDefault("GITHUB_ACCESS_TOKEN", options.GithubToken)
              .AddIfNotDefault("DEPENDABOT_REBASE_STRATEGY", update.RebaseStrategy)
              .AddIfNotDefault("DEPENDABOT_TARGET_BRANCH", update.TargetBranch)
              .AddIfNotDefault("DEPENDABOT_VENDOR", update.Vendor ? "true" : null)
              .AddIfNotDefault("DEPENDABOT_REJECT_EXTERNAL_CODE", string.Equals(update.InsecureExternalCodeExecution, "deny").ToString().ToLowerInvariant())
              .AddIfNotDefault("DEPENDABOT_VERSIONING_STRATEGY", update.VersioningStrategy)
              .AddIfNotDefault("DEPENDABOT_ALLOW_CONDITIONS", ToJson(MakeAllowEntries(update.Allow)))
              .AddIfNotDefault("DEPENDABOT_LABELS", ToJson(update.Labels))
              .AddIfNotDefault("DEPENDABOT_BRANCH_NAME_SEPARATOR", update.PullRequestBranchName?.Separator)
              .AddIfNotDefault("DEPENDABOT_MILESTONE", update.Milestone?.ToString())
              .AddIfNotDefault("DEPENDABOT_FAIL_ON_EXCEPTION", options.FailOnException.ToString().ToLowerInvariant());

        var secrets = new Dictionary<string, string>(options.Secrets) { ["DEFAULT_TOKEN"] = options.ProjectToken!, };

        // Add values for Azure DevOps
        var url = options.ProjectUrl!.Value;
        values.AddIfNotDefault("AZURE_HOSTNAME", url.Hostname)
              .AddIfNotDefault("AZURE_ORGANIZATION", url.OrganizationName)
              .AddIfNotDefault("AZURE_PROJECT", url.ProjectName)
              .AddIfNotDefault("AZURE_REPOSITORY", Uri.EscapeDataString(repository.Name!))
              .AddIfNotDefault("AZURE_ACCESS_TOKEN", options.ProjectToken)
              .AddIfNotDefault("AZURE_SET_AUTO_COMPLETE", (options.AutoComplete ?? false).ToString().ToLowerInvariant())
              .AddIfNotDefault("AZURE_AUTO_COMPLETE_IGNORE_CONFIG_IDS", ToJson(options.AutoCompleteIgnoreConfigs?.Split(';')))
              .AddIfNotDefault("AZURE_MERGE_STRATEGY", options.AutoCompleteMergeStrategy?.ToString())
              .AddIfNotDefault("AZURE_AUTO_APPROVE_PR", (options.AutoApprove ?? false).ToString().ToLowerInvariant());

        // Add extra credentials with replaced secrets
        var registries = update.Registries?.Select(r => repository.Registries[r]).ToList();
        values.AddIfNotDefault("DEPENDABOT_EXTRA_CREDENTIALS", ToJson(MakeExtraCredentials(registries, secrets)));

        return values;
    }
    internal static IList<IDictionary<string, string>>? MakeExtraCredentials(ICollection<DependabotRegistry>? registries, IDictionary<string, string> secrets)
    {
        return registries?.Select(v =>
        {
            var type = v.Type?.Replace("-", "_") ?? throw new InvalidOperationException("Type should not be null");

            var values = new Dictionary<string, string>().AddIfNotDefault("type", type);

            // values for hex-organization
            values.AddIfNotDefault("organization", v.Organization);

            // values for hex-repository
            values.AddIfNotDefault("repo", v.Repo);
            values.AddIfNotDefault("auth-key", v.AuthKey);
            values.AddIfNotDefault("public-key-fingerprint", v.PublicKeyFingerprint);

            values.AddIfNotDefault("username", v.Username);
            values.AddIfNotDefault("password", ConvertPlaceholder(v.Password, secrets));
            values.AddIfNotDefault("key", ConvertPlaceholder(v.Key, secrets));
            values.AddIfNotDefault("token", ConvertPlaceholder(v.Token, secrets));
            values.AddIfNotDefault("replaces-base", v.ReplacesBase is true ? "true" : null);

            // Some credentials do not use the 'url' property in the Ruby updater.
            // npm_registry and docker_registry use 'registry' which should be stripped off the scheme.
            // terraform_registry uses 'host' which is the hostname from the given URL.

            if (type == "docker_registry" || type == "npm_registry")
            {
                values.Add("registry", v.Url!.Replace("https://", "").Replace("http://", ""));
            }
            else if (type == "terraform_registry")
            {
                values.Add("host", new Uri(v.Url!).Host);
            }
            else
            {
                values.AddIfNotDefault("url", v.Url!);
            }
            var useRegistryProperty = type.Contains("npm") || type.Contains("docker");

            return values;
        }).ToList();
    }
    internal static string? ConvertPlaceholder(string? input, IDictionary<string, string> secrets)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;

        var result = input;
        var matches = PlaceholderPattern().Matches(input);
        foreach (var m in matches)
        {
            if (m is not Match match || !match.Success) continue;

            var placeholder = match.Value;
            var name = match.Groups[1].Value;
            if (secrets.TryGetValue(name, out var replacement))
            {
                result = result.Replace(placeholder, replacement);
            }
        }

        return result;
    }
    internal static IList<IDictionary<string, string>>? MakeAllowEntries(List<DependabotAllowDependency>? entries)
    {
        return entries?.Where(e => e.IsValid())
                       .Select(e => new Dictionary<string, string>()
                       .AddIfNotDefault("dependency-name", e.DependencyName)
                       .AddIfNotDefault("dependency-type", e.DependencyType))
                       .ToList();
    }
}

public readonly record struct UpdateRunnerState(UpdateJobStatus Status, DateTimeOffset? Start, DateTimeOffset? End)
{
    public void Deconstruct(out UpdateJobStatus status, out DateTimeOffset? start, out DateTimeOffset? end)
    {
        status = Status;
        start = Start;
        end = End;
    }
}
