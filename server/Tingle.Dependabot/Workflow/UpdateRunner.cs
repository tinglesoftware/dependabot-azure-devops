using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Azure.ResourceManager.AppContainers.Models;
using Azure.ResourceManager.Resources;
using Docker.DotNet;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tingle.Dependabot.Models.Dependabot;
using Tingle.Dependabot.Models.Management;
using Tingle.Extensions.Primitives;

namespace Tingle.Dependabot.Workflow;

internal partial class UpdateRunner(IFeatureManagerSnapshot featureManager,
                                    IOptions<WorkflowOptions> optionsAccessor,
                                    ILogger<UpdateRunner> logger)
{
    [GeneratedRegex("\\${{\\s*([a-zA-Z_]+[a-zA-Z0-9_-]*)\\s*}}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderPattern();

    private const string UpdaterContainerName = "updater";
    private const string JobDefinitionFileName = "job.json";

    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IFeatureManagerSnapshot featureManager = featureManager ?? throw new ArgumentNullException(nameof(featureManager));
    private readonly WorkflowOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly ArmClient armClient = new(new DefaultAzureCredential());
    private readonly LogsQueryClient logsQueryClient = new(new DefaultAzureCredential());
    private readonly DockerClient dockerClient = new DockerClientConfiguration().CreateClient();

    public async Task CreateAsync(Project project, Repository repository, RepositoryUpdate update, UpdateJob job, CancellationToken cancellationToken = default)
    {
        // check if debug is enabled for the project via Feature Management
        var fmc = MakeTargetingContext(project, job);
        var debug = await featureManager.IsEnabledAsync(FeatureNames.DependabotDebug, fmc);
        var useV2 = await featureManager.IsEnabledAsync(FeatureNames.UpdaterV2, fmc);

        // prepare credentials with replaced secrets
        var secrets = new Dictionary<string, string>(project.Secrets) { ["DEFAULT_TOKEN"] = project.Token!, };
        var registries = update.Registries?.Select(r => repository.Registries[r]).ToList();
        var credentials = MakeExtraCredentials(registries, secrets); // add source credentials when running the in v2
        var directory = Path.Join(options.WorkingDirectory, job.Id);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        var volumeName = "working-dir";
        var ecosystem = job.PackageEcosystem!;
        var updaterImageTag = options.GetUpdaterImageTag(ecosystem, project);
        var updaterImage = job.UpdaterImage = $"ghcr.io/tinglesoftware/dependabot-updater-{ecosystem}:{updaterImageTag}";

        var platform = job.Platform = options.JobsPlatform!.Value;
        var resourceName = job.Id;

        // if we have an existing one, there is nothing more to do
        if (platform is UpdateJobPlatform.ContainerApps)
        {
            var containerAppJobs = GetResourceGroup().GetContainerAppJobs();
            try
            {
                var response = await containerAppJobs.GetAsync(resourceName, cancellationToken);
                if (response.Value is not null) return;
            }
            catch (Azure.RequestFailedException rfe) when (rfe.Status is 404) { }
        }
        else if (platform is UpdateJobPlatform.DockerCompose)
        {
            var containers = await dockerClient.Containers.ListContainersAsync(new() { All = true, }, cancellationToken);
            var container = containers.FirstOrDefault(c => c.Names.Contains($"/{resourceName}"));
            if (container is not null) return;
        }
        else
        {
            throw new NotSupportedException($"Platform {platform} is not supported");
        }

        // write job definition file
        var experiments = new Dictionary<string, bool>
        {
            // ["record-ecosystem-versions"] = await featureManager.IsEnabledAsync(FeatureNames.RecordEcosystemVersions, fmc),
            // ["record-update-job-unknown-error"] = await featureManager.IsEnabledAsync(FeatureNames.RecordUpdateJobUnknownError, fmc),
        };
        var jobDefinitionPath = await WriteJobDefinitionAsync(project, update, job, experiments, directory, credentials, debug, cancellationToken);
        logger.WrittenJobDefinitionFile(job.Id, jobDefinitionPath);

        var env = await CreateEnvironmentVariables(project, repository, update, job, directory, credentials, debug, cancellationToken);

        if (platform is UpdateJobPlatform.ContainerApps)
        {
            // prepare the container
            var container = new ContainerAppContainer
            {
                Name = UpdaterContainerName,
                Image = updaterImage,
                Resources = job.Resources!,
                Args = { useV2 ? "update_files" : "update_script_vnext", },
                VolumeMounts = { new ContainerAppVolumeMount { VolumeName = volumeName, MountPath = "/mnt/dependabot", }, },
            };
            foreach (var (key, value) in env) container.Env.Add(new ContainerAppEnvironmentVariable { Name = key, Value = value, });

            // prepare the ContainerApp job
            var timeoutSec = Convert.ToInt32(TimeSpan.FromHours(1).TotalSeconds);
            var data = new ContainerAppJobData((project.Location ?? options.Location)!)
            {
                EnvironmentId = options.AppEnvironmentId,
                Configuration = new ContainerAppJobConfiguration(ContainerAppJobTriggerType.Manual, replicaTimeout: timeoutSec)
                {
                    ManualTriggerConfig = new JobConfigurationManualTriggerConfig
                    {
                        Parallelism = 1,
                        ReplicaCompletionCount = 1,
                    },
                    ReplicaRetryLimit = 1,
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
                    ["ecosystem"] = ecosystem,
                    ["repository"] = job.RepositorySlug,
                    ["machine-name"] = Environment.MachineName,
                },
            };
            data.Tags.AddIfNotDefault("directory", job.Directory);
            data.Tags.AddIfNotDefault("directories", ToJson(job.Directories));

            // create the ContainerApp Job
            var containerAppJobs = GetResourceGroup().GetContainerAppJobs();
            var operation = await containerAppJobs.CreateOrUpdateAsync(Azure.WaitUntil.Completed, resourceName, data, cancellationToken);
            logger.CreatedContainerAppJob(job.Id);

            // start the ContainerApp Job
            _ = await operation.Value.StartAsync(Azure.WaitUntil.Completed, cancellationToken: cancellationToken);
            logger.StartedContainerAppJob(job.Id);
        }
        else if (platform is UpdateJobPlatform.DockerCompose)
        {
            // pull the image if it does not exist
            var images = await dockerClient.Images.ListImagesAsync(new() { All = true, }, cancellationToken);
            var image = images.FirstOrDefault(i => i.RepoTags?.Contains(updaterImage) ?? false);
            if (image is null)
            {
                logger.LogInformation("Pulling image {Image}", updaterImage);
                var pullParams = new Docker.DotNet.Models.ImagesCreateParameters
                {
                    FromImage = updaterImage,
                    Tag = updaterImage.Split(':').Last(),
                };
                await dockerClient.Images.CreateImageAsync(pullParams, new(), new Progress<Docker.DotNet.Models.JSONMessage>(), cancellationToken);
            }

            // prepare the container
            var resources = job.Resources!;
            var containerParams = new Docker.DotNet.Models.CreateContainerParameters
            {
                Name = resourceName,
                Image = updaterImage,
                Tty = true,
                HostConfig = new Docker.DotNet.Models.HostConfig
                {
                    AutoRemove = false,
                    Binds = ["dependabot_working_directory:/mnt/dependabot"],
                    RestartPolicy = new Docker.DotNet.Models.RestartPolicy
                    {
                        Name = Docker.DotNet.Models.RestartPolicyKind.No,
                        MaximumRetryCount = 0,
                    },
                    NetworkMode = options.DockerNetwork,
                    Memory = ByteSize.FromGigaBytes(resources.Memory).Bytes,
                    NanoCPUs = Convert.ToInt64(resources.Cpu * 1_000_000_000),
                },
                Cmd = useV2 ? ["update_files"] : ["update_script_vnext"],
                Labels = new Dictionary<string, string?>
                {
                    ["purpose"] = "dependabot",
                    ["ecosystem"] = ecosystem,
                    ["repository"] = job.RepositorySlug,
                },
                Env = [.. env.Select(kvp => $"{kvp.Key}={kvp.Value}")],
            };
            containerParams.Labels.AddIfNotDefault("directory", job.Directory);
            containerParams.Labels.AddIfNotDefault("directories", ToJson(job.Directories));

            // create the container
            var container = await dockerClient.Containers.CreateContainerAsync(containerParams, cancellationToken);
            logger.CreatedDockerContainerJob(job.Id);

            // start the container
            var started = await dockerClient.Containers.StartContainerAsync(container.ID, new(), cancellationToken);
            if (!started) throw new InvalidOperationException($"Failed to start container {container.ID}");
            logger.StartedDockerContainerJob(job.Id);
        }
        else
        {
            throw new NotSupportedException($"Platform {platform} is not supported");
        }

        job.Status = UpdateJobStatus.Running;
    }

    public async Task DeleteAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var resourceName = job.Id;
        var platform = job.Platform;

        if (platform is UpdateJobPlatform.ContainerApps)
        {
            try
            {
                // if it does not exist, there is nothing more to do
                var containerAppJobs = GetResourceGroup().GetContainerAppJobs();
                var response = await containerAppJobs.GetAsync(resourceName, cancellationToken);
                if (response.Value is null) return;

                // delete the container group
                await response.Value.DeleteAsync(Azure.WaitUntil.Completed, cancellationToken);
            }
            catch (Azure.RequestFailedException rfe) when (rfe.Status is 404) { }
        }
        else if (platform is UpdateJobPlatform.DockerCompose)
        {
            // delete the container
            var containers = await dockerClient.Containers.ListContainersAsync(new() { All = true, }, cancellationToken);
            var container = containers.FirstOrDefault(c => c.Names.Contains($"/{resourceName}"));
            if (container is not null)
            {
                await dockerClient.Containers.RemoveContainerAsync(container.ID, new() { Force = true, }, cancellationToken);
            }
        }
        else
        {
            throw new NotSupportedException($"Platform {platform} is not supported");
        }
    }

    public async Task<UpdateRunnerState?> GetStateAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var resourceName = job.Id;
        var platform = job.Platform;

        if (platform is UpdateJobPlatform.ContainerApps)
        {
            try
            {
                // if it does not exist, there is nothing more to do
                var response = await GetResourceGroup().GetContainerAppJobAsync(resourceName, cancellationToken);
                var resource = response.Value;

                // if there is no execution, there is nothing more to do
                var executions = await resource.GetContainerAppJobExecutions().GetAllAsync(cancellationToken: cancellationToken).ToListAsync(cancellationToken: cancellationToken);
                var execution = executions.SingleOrDefault();
                if (execution is null) return null;

                var status = execution.Data.Status.ToString() switch
                {
                    "Succeeded" => UpdateJobStatus.Succeeded,
                    "Running" => UpdateJobStatus.Running,
                    "Processing" => UpdateJobStatus.Running,
                    _ => UpdateJobStatus.Failed,
                };

                // there is no state for jobs that are running
                if (status is UpdateJobStatus.Running) return null;

                // get the period
                DateTimeOffset? start = execution.Data.StartOn, end = execution.Data.EndOn;

                // delete the job directory
                DeleteJobDirectory(job);

                // create and return state
                return new UpdateRunnerState(status, start, end);
            }
            catch (Azure.RequestFailedException rfe) when (rfe.Status is 404) { }
        }
        else if (platform is UpdateJobPlatform.DockerCompose)
        {
            var containers = await dockerClient.Containers.ListContainersAsync(new() { All = true, }, cancellationToken);
            var container = containers.FirstOrDefault(c => c.Names.Contains($"/{resourceName}"));
            if (container is null) return null;

            var status = container.State.ToString() switch
            {
                "created" => UpdateJobStatus.Running,
                "running" => UpdateJobStatus.Running,
                "exited" => UpdateJobStatus.Succeeded,
                _ => UpdateJobStatus.Failed,
            };

            // there is no state for jobs that are running
            if (status is UpdateJobStatus.Running) return null;

            // get the period
            var inspection = await dockerClient.Containers.InspectContainerAsync(container.ID, cancellationToken);
            var start = DateTimeOffset.Parse(inspection.State.StartedAt);
            var end = DateTimeOffset.Parse(inspection.State.FinishedAt);

            // delete the job directory
            DeleteJobDirectory(job);

            // create and return state
            return new UpdateRunnerState(status, start, end);
        }
        else
        {
            throw new NotSupportedException($"Platform {platform} is not supported");
        }
        return null;
    }

    public async Task<string?> GetLogsAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var logs = (string?)null;
        var resourceName = job.Id;
        var platform = job.Platform;

        if (platform is UpdateJobPlatform.ContainerApps)
        {
            // pull logs from Log Analytics
            var query = $"ContainerAppConsoleLogs_CL | where ContainerJobName_s == '{resourceName}' | order by _timestamp_d asc | project Log_s";
            var response = await logsQueryClient.QueryWorkspaceAsync<string>(workspaceId: options.LogAnalyticsWorkspaceId,
                                                                             query: query,
                                                                             timeRange: QueryTimeRange.All,
                                                                             cancellationToken: cancellationToken);

            logs = string.Join(Environment.NewLine, response.Value);
        }
        else if (platform is UpdateJobPlatform.DockerCompose)
        {
            // pull docker container logs
            var containerId = resourceName;
            var logParams = new Docker.DotNet.Models.ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = false,
                Timestamps = false,
            };
            using var stream = await dockerClient.Containers.GetContainerLogsAsync(containerId, tty: true, logParams, cancellationToken);
            using var stdin = new MemoryStream();
            using var stdout = new MemoryStream();
            using var stderr = new MemoryStream();

            await stream.CopyOutputToAsync(stdin, stdout, stderr, cancellationToken);
            stdout.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(stdout);
            logs = await reader.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(logs))
            {
                stderr.Seek(0, SeekOrigin.Begin);
                using var errorReader = new StreamReader(stderr);
                logs = await errorReader.ReadToEndAsync(cancellationToken);
            }
        }
        else
        {
            throw new NotSupportedException($"Platform {platform} is not supported");
        }

        return logs;
    }

    internal async Task<IDictionary<string, string>> CreateEnvironmentVariables(Project project,
                                                                                Repository repository,
                                                                                RepositoryUpdate update,
                                                                                UpdateJob job,
                                                                                string directory,
                                                                                IList<Dictionary<string, string>> credentials,
                                                                                bool debug,
                                                                                CancellationToken cancellationToken = default) // TODO: unit test this
    {
        // Add compulsory values
        var values = new Dictionary<string, string>
        {
            // env for v2
            ["DEPENDABOT_JOB_ID"] = job.Id!,
            ["DEPENDABOT_JOB_TOKEN"] = job.AuthKey!,
            ["DEPENDABOT_DEBUG"] = debug.ToString().ToLower(),
            ["DEPENDABOT_API_URL"] = options.JobsApiUrl!.ToString(),
            ["DEPENDABOT_JOB_PATH"] = Path.Join(directory, JobDefinitionFileName),
            ["DEPENDABOT_OUTPUT_PATH"] = Path.Join(directory, "output"),
            // Setting DEPENDABOT_REPO_CONTENTS_PATH causes some issues, ignore till we can resolve
            //["DEPENDABOT_REPO_CONTENTS_PATH"] = Path.Join(jobDirectory, "repo"),
            ["GITHUB_ACTIONS"] = "false",
            ["UPDATER_DETERMINISTIC"] = "true",

            // env for v1
            ["DEPENDABOT_PACKAGE_MANAGER"] = job.PackageEcosystem!,
            ["DEPENDABOT_OPEN_PULL_REQUESTS_LIMIT"] = update.OpenPullRequestsLimit.ToString(),
            ["DEPENDABOT_EXTRA_CREDENTIALS"] = ToJson(credentials),
            ["DEPENDABOT_FAIL_ON_EXCEPTION"] = "false", // we the script to run to completion so that we get notified of job completion
        };

        // Add optional values
        values.AddIfNotDefault("GITHUB_ACCESS_TOKEN", project.GithubToken ?? options.GithubToken)
              .AddIfNotDefault("DEPENDABOT_REBASE_STRATEGY", update.RebaseStrategy)
              .AddIfNotDefault("DEPENDABOT_DIRECTORY", update.Directory)
              .AddIfNotDefault("DEPENDABOT_DIRECTORIES", ToJson(update.Directories))
              .AddIfNotDefault("DEPENDABOT_TARGET_BRANCH", update.TargetBranch)
              .AddIfNotDefault("DEPENDABOT_VENDOR", update.Vendor ? "true" : null)
              .AddIfNotDefault("DEPENDABOT_REJECT_EXTERNAL_CODE", string.Equals(update.InsecureExternalCodeExecution, "deny").ToString().ToLowerInvariant())
              .AddIfNotDefault("DEPENDABOT_VERSIONING_STRATEGY", update.VersioningStrategy)
              .AddIfNotDefault("DEPENDABOT_DEPENDENCY_GROUPS", ToJson(update.Groups))
              .AddIfNotDefault("DEPENDABOT_ALLOW_CONDITIONS", ToJson(update.Allow))
              .AddIfNotDefault("DEPENDABOT_IGNORE_CONDITIONS", ToJson(update.Ignore))
              .AddIfNotDefault("DEPENDABOT_COMMIT_MESSAGE_OPTIONS", ToJson(update.CommitMessage))
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
                                                        bool debug,
                                                        CancellationToken cancellationToken = default) // TODO: unit test this
    {
        [return: NotNullIfNotNull(nameof(value))]
        static JsonNode? ToJsonNode<T>(T? value) => value is null ? null : JsonSerializer.SerializeToNode(value, serializerOptions); // null ensures we do not add to the values

        var url = project.Url;
        var credentialsMetadata = MakeCredentialsMetadata(credentials);

        var definition = new JsonObject
        {
            ["job"] = new JsonObject
            {
                ["dependency-groups"] = ToJsonNode(update.Groups ?? []),
                ["allowed-updates"] = ToJsonNode(update.Allow ?? []),
                ["credentials-metadata"] = ToJsonNode(credentialsMetadata).AsArray(),
                // ["dependencies"] = null, // object array
                ["directory"] = job.Directory,
                ["directories"] = ToJsonNode(job.Directories),
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
                    ["directories"] = ToJsonNode(job.Directories),
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

            // if no host, pull host from url, index-url, or registry if available
            if (!cred.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host))
            {
                if (cred.TryGetValue("url", out var url) || cred.TryGetValue("index-url", out url)) { }
                else if (cred.TryGetValue("registry", out var registry)) url = $"https://{registry}";

                if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out var u))
                {
                    host = u.Host;
                }
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

            /*
             * Some credentials do not use the 'url' property in the Ruby updater.
             * The 'host' and 'registry' properties are derived from the given URL.
             * The 'registry' property is derived from the 'url' by stripping off the scheme.
             * The 'host' property is derived from the hostname of the 'url'.
             *
             * 'npm_registry' and 'docker_registry' use 'registry' only.
             * 'terraform_registry' uses 'host' only.
             * 'composer_repository' uses both 'url' and 'host'.
             * 'python_index' uses 'index-url' instead of 'url'.
            */

            if (Uri.TryCreate(v.Url, UriKind.Absolute, out var url))
            {
                var addRegistry = type is "docker_registry" or "npm_registry";
                if (addRegistry) values.Add("registry", $"{url.Host}{url.PathAndQuery}".TrimEnd('/'));

                var addHost = type is "terraform_registry" or "composer_repository";
                if (addHost) values.Add("host", url.Host);
            }

            if (type is "python_index") values.AddIfNotDefault("index-url", v.Url);

            var skipUrl = type is "docker_registry" or "npm_registry" or "terraform_registry" or "python_index";
            if (!skipUrl) values.AddIfNotDefault("url", v.Url);

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

    internal void DeleteJobDirectory(UpdateJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        // delete the job directory if it exists
        var jobDirectory = Path.Join(options.WorkingDirectory, job.Id);
        if (Directory.Exists(jobDirectory))
        {
            Directory.Delete(jobDirectory, recursive: true);
        }
    }

    private ResourceGroupResource GetResourceGroup() => armClient.GetResourceGroupResource(new(options.ResourceGroupId!));

    [return: NotNullIfNotNull(nameof(value))]
    private static string? ToJson<T>(T? value) => value is null ? null : JsonSerializer.Serialize(value, serializerOptions); // null ensures we do not add to the values
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
