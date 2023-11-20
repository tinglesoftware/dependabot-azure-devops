using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;

namespace Tingle.Dependabot.Workflow;

internal partial class UpdateRunner
{
    [GeneratedRegex("\\${{\\s*([a-zA-Z_]+[a-zA-Z0-9_-]*)\\s*}}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderPattern();

    private const string UpdaterContainerName = "updater";
    private const string JobDefinitionFileName = "job.json";

    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IFeatureManagerSnapshot featureManager;
    private readonly WorkflowOptions options;
    private readonly ILogger logger;

    private readonly ArmClient armClient;
    private readonly ResourceGroupResource resourceGroup;
    private readonly LogsQueryClient logsQueryClient;

    public UpdateRunner(IFeatureManagerSnapshot featureManager, IOptions<WorkflowOptions> optionsAccessor, ILogger<UpdateRunner> logger)
    {
        this.featureManager = featureManager ?? throw new ArgumentNullException(nameof(featureManager));
        options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        armClient = new ArmClient(new DefaultAzureCredential());
        resourceGroup = armClient.GetResourceGroupResource(new(options.ResourceGroupId!));
        logsQueryClient = new LogsQueryClient(new DefaultAzureCredential());
    }

    public async Task CreateAsync(Project project, Repository repository, RepositoryUpdate update, UpdateJob job, CancellationToken cancellationToken = default)
    {
        var resourceName = job.Id;

        // if we have an existing one, there is nothing more to do
        var containerAppJobs = resourceGroup.GetContainerAppJobs();
        try
        {
            var response = await containerAppJobs.GetAsync(resourceName, cancellationToken);
            if (response.Value is not null) return;
        }
        catch (Azure.RequestFailedException rfe) when (rfe.Status is 404) { }

        // check if V2 updater is enabled for the project via Feature Management
        var fmc = MakeTargetingContext(project, job);
        var useV2 = await featureManager.IsEnabledAsync(FeatureNames.UpdaterV2, fmc);

        // prepare credentials with replaced secrets
        var secrets = new Dictionary<string, string>(project.Secrets) { ["DEFAULT_TOKEN"] = project.Token!, };
        var registries = update.Registries?.Select(r => repository.Registries[r]).ToList();
        var credentials = MakeExtraCredentials(registries, secrets); // add source credentials when running the in v2
        var directory = Path.Join(options.WorkingDirectory, job.Id);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        // prepare the container
        var volumeName = "working-dir";
        var container = new ContainerAppContainer
        {
            Name = UpdaterContainerName,
            Image = $"ghcr.io/tinglesoftware/dependabot-updater-{job.PackageEcosystem}:{project.UpdaterImageTag ?? options.UpdaterImageTag}",
            Resources = job.Resources!,
            Args = { useV2 ? "update_files" : "update_script", },
            VolumeMounts = { new ContainerAppVolumeMount { VolumeName = volumeName, MountPath = options.WorkingDirectory, }, },
        };
        var env = await CreateEnvironmentVariables(project, repository, update, job, directory, credentials, cancellationToken);
        foreach (var (key, value) in env) container.Env.Add(new ContainerAppEnvironmentVariable { Name = key, Value = value, });

        // prepare the ContainerApp job
        var data = new ContainerAppJobData((project.Location ?? options.Location)!)
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
                ["repository"] = job.RepositorySlug,
                ["directory"] = job.Directory,
                ["machine-name"] = Environment.MachineName,
            },
        };

        // write job definition file
        var experiments = new Dictionary<string, bool>
        {
            // ["record-ecosystem-versions"] = await featureManager.IsEnabledAsync(FeatureNames.RecordEcosystemVersions, fmc),
            // ["record-update-job-unknown-error"] = await featureManager.IsEnabledAsync(FeatureNames.RecordUpdateJobUnknownError, fmc),
        };
        var jobDefinitionPath = await WriteJobDefinitionAsync(project, update, job, experiments, directory, credentials, cancellationToken);
        logger.WrittenJobDefinitionFile(job.Id, jobDefinitionPath);

        // create the ContainerApp Job
        var operation = await containerAppJobs.CreateOrUpdateAsync(Azure.WaitUntil.Completed, resourceName, data, cancellationToken);
        logger.CreatedContainerAppJob(job.Id);

        // start the ContainerApp Job
        _ = await operation.Value.StartAsync(Azure.WaitUntil.Completed, cancellationToken: cancellationToken);
        logger.StartedContainerAppJob(job.Id);
        job.Status = UpdateJobStatus.Running;
    }

    public async Task DeleteAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var resourceName = job.Id;

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
        var resourceName = job.Id;

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
        var resourceName = job.Id;

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

    internal async Task<IDictionary<string, string>> CreateEnvironmentVariables(Project project,
                                                                                Repository repository,
                                                                                RepositoryUpdate update,
                                                                                UpdateJob job,
                                                                                string directory,
                                                                                IList<Dictionary<string, string>> credentials,
                                                                                CancellationToken cancellationToken = default) // TODO: unit test this
    {
        [return: NotNullIfNotNull(nameof(value))]
        static string? ToJson<T>(T? value) => value is null ? null : JsonSerializer.Serialize(value, serializerOptions); // null ensures we do not add to the values

        // check if debug and determinism is enabled for the project via Feature Management
        var fmc = MakeTargetingContext(project, job);
        var debugAllJobs = await featureManager.IsEnabledAsync(FeatureNames.DebugAllJobs); // context is not passed because this is global
        var deterministic = await featureManager.IsEnabledAsync(FeatureNames.DeterministicUpdates, fmc);

        // Add compulsory values
        var values = new Dictionary<string, string>
        {
            // env for v2
            ["DEPENDABOT_JOB_ID"] = job.Id!,
            ["DEPENDABOT_JOB_TOKEN"] = job.AuthKey!,
            ["DEPENDABOT_DEBUG"] = debugAllJobs.ToString().ToLower(),
            ["DEPENDABOT_API_URL"] = options.JobsApiUrl!.ToString(),
            ["DEPENDABOT_JOB_PATH"] = Path.Join(directory, JobDefinitionFileName),
            ["DEPENDABOT_OUTPUT_PATH"] = Path.Join(directory, "output"),
            // Setting DEPENDABOT_REPO_CONTENTS_PATH causes some issues, ignore till we can resolve
            //["DEPENDABOT_REPO_CONTENTS_PATH"] = Path.Join(jobDirectory, "repo"),
            ["GITHUB_ACTIONS"] = "false",
            ["UPDATER_DETERMINISTIC"] = deterministic.ToString().ToLower(),

            // env for v1
            ["DEPENDABOT_PACKAGE_MANAGER"] = job.PackageEcosystem!,
            ["DEPENDABOT_DIRECTORY"] = job.Directory!,
            ["DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT"] = update.OpenPullRequestsLimit.ToString(),
            ["DEPENDABOT_EXTRA_CREDENTIALS"] = ToJson(credentials),
            ["DEPENDABOT_FAIL_ON_EXCEPTION"] = "false", // we the script to run to completion so that we get notified of job completion
        };

        // Add optional values
        values.AddIfNotDefault("GITHUB_ACCESS_TOKEN", project.GithubToken ?? options.GithubToken)
              .AddIfNotDefault("DEPENDABOT_REBASE_STRATEGY", update.RebaseStrategy)
              .AddIfNotDefault("DEPENDABOT_TARGET_BRANCH", update.TargetBranch)
              .AddIfNotDefault("DEPENDABOT_VENDOR", update.Vendor ? "true" : null)
              .AddIfNotDefault("DEPENDABOT_REJECT_EXTERNAL_CODE", string.Equals(update.InsecureExternalCodeExecution, "deny").ToString().ToLowerInvariant())
              .AddIfNotDefault("DEPENDABOT_VERSIONING_STRATEGY", update.VersioningStrategy)
              .AddIfNotDefault("DEPENDABOT_ALLOW_CONDITIONS", ToJson(update.Allow))
              .AddIfNotDefault("DEPENDABOT_LABELS", ToJson(update.Labels))
              .AddIfNotDefault("DEPENDABOT_BRANCH_NAME_SEPARATOR", update.PullRequestBranchName?.Separator)
              .AddIfNotDefault("DEPENDABOT_MILESTONE", update.Milestone?.ToString());

        // Add values for Azure DevOps
        var url = project.Url;
        values.AddIfNotDefault("AZURE_HOSTNAME", url.Hostname)
              .AddIfNotDefault("AZURE_ORGANIZATION", url.OrganizationName)
              .AddIfNotDefault("AZURE_PROJECT", url.ProjectName)
              .AddIfNotDefault("AZURE_REPOSITORY", Uri.EscapeDataString(repository.Name!))
              .AddIfNotDefault("AZURE_ACCESS_TOKEN", project.Token)
              .AddIfNotDefault("AZURE_SET_AUTO_COMPLETE", project.AutoComplete.Enabled.ToString().ToLowerInvariant())
              .AddIfNotDefault("AZURE_AUTO_COMPLETE_IGNORE_CONFIG_IDS", ToJson(project.AutoComplete.IgnoreConfigs ?? []))
              .AddIfNotDefault("AZURE_MERGE_STRATEGY", project.AutoComplete.MergeStrategy?.ToString())
              .AddIfNotDefault("AZURE_AUTO_APPROVE_PR", project.AutoApprove.Enabled.ToString().ToLowerInvariant());

        return values;
    }

    internal async Task<string> WriteJobDefinitionAsync(Project project,
                                                        RepositoryUpdate update,
                                                        UpdateJob job,
                                                        IDictionary<string, bool> experiments,
                                                        string directory,
                                                        IList<Dictionary<string, string>> credentials,
                                                        CancellationToken cancellationToken = default) // TODO: unit test this
    {
        [return: NotNullIfNotNull(nameof(value))]
        static JsonNode? ToJsonNode<T>(T? value) => value is null ? null : JsonSerializer.SerializeToNode(value, serializerOptions); // null ensures we do not add to the values

        var url = project.Url;
        var credentialsMetadata = MakeCredentialsMetadata(credentials);

        // check if debug is enabled for the project via Feature Management
        var fmc = MakeTargetingContext(project, job);
        var debug = await featureManager.IsEnabledAsync(FeatureNames.DebugJobs, fmc);

        var definition = new JsonObject
        {
            ["job"] = new JsonObject
            {
                ["allowed-updates"] = ToJsonNode(update.Allow ?? []),
                ["credentials-metadata"] = ToJsonNode(credentialsMetadata).AsArray(),
                // ["dependencies"] = null, // object array
                ["directory"] = job.Directory,
                // ["existing-pull-requests"] = null, // object array
                ["experiments"] = ToJsonNode(experiments),
                ["ignore-conditions"] = ToJsonNode(update.Ignore ?? []),
                // ["security-advisories"] = null, // object array
                ["package_manager"] = ConvertEcosystemToPackageManager(job.PackageEcosystem!),
                ["repo-name"] = job.RepositorySlug,
                ["source"] = new JsonObject
                {
                    ["provider"] = "azure",
                    ["repo"] = job.RepositorySlug,
                    ["directory"] = job.Directory,
                    ["branch"] = update.TargetBranch,
                    ["hostname"] = url.Hostname,
                    ["api-endpoint"] = new UriBuilder
                    {
                        Scheme = Uri.UriSchemeHttps,
                        Host = url.Hostname,
                        Port = url.Port ?? -1,
                    }.ToString(),
                },
                ["lockfile-only"] = update.VersioningStrategy == "lockfile-only",
                ["requirements-update-strategy"] = update.VersioningStrategy?.Replace("-", "_"),
                // ["update-subdependencies"] = false,
                // ["updating-a-pull-request"] = false,
                ["vendor-dependencies"] = update.Vendor,
                ["security-updates-only"] = update.OpenPullRequestsLimit == 0,
                ["debug"] = debug,
            },
            ["credentials"] = ToJsonNode(credentials).AsArray(),
        };

        // write the job definition file
        var path = Path.Join(directory, JobDefinitionFileName);
        if (File.Exists(path)) File.Delete(path);
        using var stream = File.OpenWrite(path);
        await JsonSerializer.SerializeAsync(stream, definition, serializerOptions, cancellationToken);

        return path;
    }

    internal static TargetingContext MakeTargetingContext(Project project, UpdateJob job)
    {
        return new TargetingContext
        {
            Groups = new[]
            {
                $"provider:{project.Type.ToString().ToLower()}",
                $"project:{project.Id}",
                $"ecosystem:{job.PackageEcosystem}",
            },
        };
    }
    internal static IList<Dictionary<string, string>> MakeCredentialsMetadata(IList<Dictionary<string, string>> credentials)
    {
        return credentials.Select(cred =>
        {
            var values = new Dictionary<string, string> { ["type"] = cred["type"], };
            cred.TryGetValue("host", out var host);

            // pull host from registry if available
            if (string.IsNullOrWhiteSpace(host))
            {
                host = cred.TryGetValue("registry", out var registry) && Uri.TryCreate($"https://{registry}", UriKind.Absolute, out var u) ? u.Host : host;
            }

            // pull host from registry if url
            if (string.IsNullOrWhiteSpace(host))
            {
                host = cred.TryGetValue("url", out var url) && Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : host;
            }

            values.AddIfNotDefault("host", host);

            return values;
        }).ToList();
    }
    internal static IList<Dictionary<string, string>> MakeExtraCredentials(ICollection<DependabotRegistry>? registries, IDictionary<string, string> secrets)
    {
        if (registries is null) return Array.Empty<Dictionary<string, string>>();

        return registries.Select(v =>
        {
            var type = v.Type?.Replace("-", "_") ?? throw new InvalidOperationException("Type should not be null");

            var values = new Dictionary<string, string> { ["type"] = type, };

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
    internal static string? ConvertEcosystemToPackageManager(string ecosystem)
    {
        ArgumentException.ThrowIfNullOrEmpty(ecosystem);

        return ecosystem switch
        {
            "github-actions" => "github_actions",
            "gitsubmodule" => "submodules",
            "gomod" => "go_modules",
            "mix" => "hex",
            "npm" => "npm_and_yarn",
            // Additional ones
            "yarn" => "npm_and_yarn",
            "pipenv" => "pip",
            "pip-compile" => "pip",
            "poetry" => "pip",
            _ => ecosystem,
        };
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
