using Azure.Identity;
using Docker.DotNet;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Tingle.Dependabot.Models.Management;
using Tingle.Extensions.Primitives;

namespace Tingle.Dependabot.Workflow;

internal partial class UpdateRunner(IFeatureManagerSnapshot featureManager,
                                    ConfigFilesWriter configFilesWriter,
                                    IOptions<WorkflowOptions> optionsAccessor,
                                    ILogger<UpdateRunner> logger)
{
    private const string ContainerNameProxy = "proxy";
    private const string ContainerNameUpdater = "updater";
    private const string VolumeNameCerts = "certs";
    private const string VolumeNameJobs = "jobs";
    private const string VolumeNameProxy = "proxy";
    private const string MountPathJobs = "/mnt/dependabot/jobs";
    private const string MountPathCert = "/usr/local/share/ca-certificates/dbot-ca.crt";
    private const int ProxyPort = 1080;

    private readonly IFeatureManagerSnapshot featureManager = featureManager ?? throw new ArgumentNullException(nameof(featureManager));
    private readonly WorkflowOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly DockerClient dockerClient = new DockerClientConfiguration().CreateClient();

    public async Task CreateAsync(UpdaterContext context, CancellationToken cancellationToken = default)
    {
        var project = context.Project;
        var update = context.Update;
        var job = context.Job;

        // if we have an existing one, there is nothing more to do
        var ecosystem = job.PackageEcosystem;
        var resourceName = job.ResourceName;
        var containers = await dockerClient.Containers.ListContainersAsync(new() { All = true, }, cancellationToken);
        var container = containers.FirstOrDefault(c => c.Names.Contains($"/{resourceName}"));
        if (container is not null) return;

        // check if debug is enabled for the project via Feature Management
        var fmc = MakeTargetingContext(project, job);
        var debug = await featureManager.IsEnabledAsync(FeatureNames.DependabotDebug, fmc);

        // prepare credentials and job directory
        var credentials = configFilesWriter.MakeCredentials(context);

        // write proxy config file
        var proxyDirectory = Path.Join(options.ProxyDirectory, job.Id);
        if (!Directory.Exists(proxyDirectory)) Directory.CreateDirectory(proxyDirectory);
        var proxyConfigPath = Path.Join(proxyDirectory, "config.json");
        if (File.Exists(proxyConfigPath)) File.Delete(proxyConfigPath);
        var proxyConfigContext = new ProxyConfigContext(context, credentials);
        await configFilesWriter.WriteProxyAsync(proxyConfigPath, proxyConfigContext, cancellationToken);

        // the proxy config must be mounted in the root
        // example: change /Users/maxwell/Documents/dependabot-azure-devops/server/Tingle.Dependabot/work/proxy/1359497145993567115/config.json
        //              to 1359497145993567115/config.json
        var mountedProxyConfigSubPath = Path.GetRelativePath(options.ProxyDirectory!, proxyConfigPath);

        // write job definition file
        var jobsDirectory = Path.Join(options.JobsDirectory, job.Id);
        if (!Directory.Exists(jobsDirectory)) Directory.CreateDirectory(jobsDirectory);
        var jobDefinitionPath = Path.Join(jobsDirectory, "job.json");
        if (File.Exists(jobDefinitionPath)) File.Delete(jobDefinitionPath);
        var outputPath = Path.Join(jobsDirectory, "output.json");
        if (File.Exists(outputPath)) File.Delete(outputPath);

        var jobConfigContext = new JobConfigContext(context, credentials, debug);
        await configFilesWriter.WriteJobAsync(jobDefinitionPath, jobConfigContext, cancellationToken);
        var caCertPath = Path.Join(options.CertsDirectory, "cert.crt");

        // the job path we have might local to the machine but we need to be based on the mount we have in the container
        // example: change /Users/maxwell/Documents/dependabot-azure-devops/server/Tingle.Dependabot/work/jobs/1359497145993567115/job.json
        //              to /mnt/dependabot/jobs/1359497145993567115/job.json
        // example: change /Users/maxwell/Documents/dependabot-azure-devops/server/Tingle.Dependabot/work/jobs/1359497145993567115/output.json
        //              to /mnt/dependabot/jobs/1359497145993567115/output.json
        var mountedJobDefinitionPath = Path.Combine(MountPathJobs, Path.GetRelativePath(options.JobsDirectory!, jobDefinitionPath));
        var mountedOutputPath = Path.Combine(MountPathJobs, Path.GetRelativePath(options.JobsDirectory!, outputPath));

        // https://github.com/dependabot/cli/blob/main/internal/infra/proxy.go
        var proxyImage = $"ghcr.io/github/dependabot-update-job-proxy/dependabot-update-job-proxy:{options.ProxyImageTag}";
        var proxyEntrypoint = new List<string> { "sh", "-c", "update-ca-certificates && /update-job-proxy" };
        var proxyEnv = new Dictionary<string, string>
        {
            ["JOB_ID"] = job.Id!,
            ["PROXY_CACHE"] = "true",
            ["LOG_RESPONSE_BODY_ON_AUTH_FAILURE"] = "true",
        };

        // we use command and not args to override the default one in ghcr.io./dependabot/dependabot-updater-{ecosystem} images
        // https://github.com/dependabot/cli/blob/main/internal/infra/run.go
        // https://github.com/dependabot/cli/blob/main/internal/infra/updater.go
        var updaterImage = $"ghcr.io/dependabot/dependabot-updater-{ecosystem}:{options.UpdaterImageTag}";
        var updaterCommand = new List<string> { "/bin/sh", "-c", "update-ca-certificates && bin/run fetch_files && bin/run update_files" };
        var updaterEnv = new Dictionary<string, string>
        {
            ["GITHUB_ACTIONS"] = "true", // sets exit code when fetch fails
            ["DEPENDABOT_JOB_ID"] = job.Id!,
            ["DEPENDABOT_JOB_TOKEN"] = $"Updater {job.AuthKey}",
            ["DEPENDABOT_JOB_PATH"] = mountedJobDefinitionPath,
            ["DEPENDABOT_OUTPUT_PATH"] = mountedOutputPath,
            ["DEPENDABOT_REPO_CONTENTS_PATH"] = "/home/dependabot/dependabot-updater/repo",
            ["DEPENDABOT_API_URL"] = $"{options.JobsApiUrl}".TrimEnd('/'), // make sure to remove trailing slash so we match incoming requests
            ["UPDATER_ONE_CONTAINER"] = "true",
            ["UPDATER_DETERMINISTIC"] = "true",
            ["DEPENDABOT_DEBUG"] = debug.ToString().ToLower(),
        };
        var updateEnvNamesForProxyUrl = new List<string> { "http_proxy", "HTTP_PROXY", "https_proxy", "HTTPS_PROXY" };

        // fetch networks (should we cache this ?)
        var networks = await GetDockerNetworksAsync(cancellationToken);
        var serverNetwork = networks[DockerNetworkName.Server];
        var proxyNetwork = networks[DockerNetworkName.Proxy];
        var jobsNetwork = networks[DockerNetworkName.Jobs];

        // find and pull images we need
        job.ProxyImage = await GetImageWithDigestAsync(proxyImage, true, cancellationToken);
        job.UpdaterImage = await GetImageWithDigestAsync(updaterImage, true, cancellationToken);

        // prepare the proxy container
        var proxyContainerParams = new Docker.DotNet.Models.CreateContainerParameters
        {
            Name = job.ResourceNameProxy,
            Image = job.ProxyImage,
            Tty = true,
            HostConfig = new Docker.DotNet.Models.HostConfig
            {
                AutoRemove = false,
                // TODO: make sure this bind works from within a container not just the local machine
                Binds = [$"{proxyConfigPath}:/config.json"],
                RestartPolicy = new Docker.DotNet.Models.RestartPolicy { Name = Docker.DotNet.Models.RestartPolicyKind.No },
                Memory = ByteSize.FromMegaBytes(100).Bytes, // 100M
                NanoCPUs = Convert.ToInt64(0.1 * 1_000_000_000), // 100m
                ExtraHosts = ["host.docker.internal:host-gateway"], // manual mapping needed for Docker on Linux
            },
            NetworkingConfig = new Docker.DotNet.Models.NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, Docker.DotNet.Models.EndpointSettings>
                {
                    [proxyNetwork.Name] = new() { NetworkID = proxyNetwork.Id, }, // where the proxies live
                    [serverNetwork.Name] = new() { NetworkID = serverNetwork.Id, }, // allow the proxy to reach the API
                    [jobsNetwork.Name] = new() { NetworkID = jobsNetwork.Id, }, // allow the update job to reach the proxy
                }
            },
            Entrypoint = proxyEntrypoint,
            Env = [.. proxyEnv.Select(kvp => $"{kvp.Key}={kvp.Value}")],
        };

        // create the proxy container
        var proxyContainer = await dockerClient.Containers.CreateContainerAsync(proxyContainerParams, cancellationToken);
        logger.CreatedUpdaterProxy(job.Id);

        // // mounting the config.json file is so much gymnastics so we instead just write it into the container
        // using var tarStream = new MemoryStream();
        // using (var tarWriter = new TarWriter(tarStream, leaveOpen: true))
        // {
        //     var entry = new PaxTarEntry(TarEntryType.RegularFile, Path.GetFileName(proxyConfigPath))
        //     {
        //         DataStream = new MemoryStream(await File.ReadAllBytesAsync(proxyConfigPath, cancellationToken)),
        //     };
        //     await tarWriter.WriteEntryAsync(entry, cancellationToken: cancellationToken);
        // }
        // tarStream.Seek(0, SeekOrigin.Begin);
        // await dockerClient.Containers.ExtractArchiveToContainerAsync(proxyContainer.ID, new() { Path = "/" }, tarStream, cancellationToken);

        // start the proxy container
        _ = await dockerClient.Containers.StartContainerAsync(proxyContainer.ID, new(), cancellationToken);
        logger.StartedUpdaterProxy(job.Id);

        var proxyInspection = await dockerClient.Containers.InspectContainerAsync(proxyContainer.ID, cancellationToken);
        var proxyUrl = $"http://{proxyInspection.NetworkSettings.Networks[jobsNetwork.Name].IPAddress}:{ProxyPort}";

        // prepare the updater container
        var updaterResources = job.Resources!;
        var updaterContainerParams = new Docker.DotNet.Models.CreateContainerParameters
        {
            Name = resourceName,
            Image = job.UpdaterImage,
            Tty = true,
            HostConfig = new Docker.DotNet.Models.HostConfig
            {
                AutoRemove = false,
                Binds = [
                    $"{(options.IsInContainer ? "dependabot_jobs" : options.JobsDirectory)}:{MountPathJobs}",
                    // TODO: make sure this bind works from within a container not just the local machine
                    $"{caCertPath}:{MountPathCert}",
                ],
                RestartPolicy = new Docker.DotNet.Models.RestartPolicy { Name = Docker.DotNet.Models.RestartPolicyKind.No },
                Memory = ByteSize.FromGigaBytes(updaterResources.Memory).Bytes,
                NanoCPUs = Convert.ToInt64(updaterResources.Cpu * 1_000_000_000),
            },
            NetworkingConfig = new Docker.DotNet.Models.NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, Docker.DotNet.Models.EndpointSettings>
                {
                    [jobsNetwork.Name] = new() { NetworkID = jobsNetwork.Id, } // where the update jobs live
                }
            },
            Cmd = updaterCommand,
            Env = [.. updaterEnv.Select(kvp => $"{kvp.Key}={kvp.Value}")],
        };
        foreach (var name in updateEnvNamesForProxyUrl) updaterContainerParams.Env.Add($"{name}={proxyUrl}");

        // create the updater container
        var updaterContainer = await dockerClient.Containers.CreateContainerAsync(updaterContainerParams, cancellationToken);
        logger.CreatedUpdaterJob(job.Id);

        // start the updater container
        _ = await dockerClient.Containers.StartContainerAsync(updaterContainer.ID, new(), cancellationToken);
        logger.StartedUpdaterJob(job.Id);

        job.Status = UpdateJobStatus.Running;
    }

    public async Task DeleteAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var resourceName = job.ResourceName;

        // delete the container
        var containers = await dockerClient.Containers.ListContainersAsync(new() { All = true, }, cancellationToken);
        var container = containers.FirstOrDefault(c => c.Names.Contains($"/{resourceName}"));
        if (container is not null)
        {
            await dockerClient.Containers.RemoveContainerAsync(container.ID, new() { Force = true, }, cancellationToken);
        }
        container = containers.FirstOrDefault(c => c.Names.Contains($"/{job.ResourceNameProxy}"));
        if (container is not null)
        {
            await dockerClient.Containers.RemoveContainerAsync(container.ID, new() { Force = true, }, cancellationToken);
        }
    }

    public async Task<UpdateRunnerState?> GetStateAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var resourceName = job.ResourceName;

        var containers = await dockerClient.Containers.ListContainersAsync(new() { All = true, }, cancellationToken);
        var container = containers.FirstOrDefault(c => c.Names.Contains($"/{resourceName}"));
        if (container is null) return null;

        var inspection = await dockerClient.Containers.InspectContainerAsync(container.ID, cancellationToken);
        var status = inspection.State switch
        {
            { Running: true } => UpdateJobStatus.Running,
            { Status: "created" } => UpdateJobStatus.Running,
            { Status: "exited", ExitCode: 0 } => UpdateJobStatus.Succeeded,
            { Status: "exited", ExitCode: not 0 } => UpdateJobStatus.Failed,
            _ => UpdateJobStatus.Failed,
        };

        // there is no state for jobs that are running
        if (status is UpdateJobStatus.Running) return null;

        // get the period
        var start = DateTimeOffset.Parse(inspection.State.StartedAt);
        var end = DateTimeOffset.Parse(inspection.State.FinishedAt);

        // delete the job directory
        DeleteJobDirectory(job);

        // create and return state
        return new UpdateRunnerState(status, start, end);
    }

    public async Task<string?> GetLogsAsync(UpdateJob job, CancellationToken cancellationToken = default)
    {
        var logs = (string?)null;
        var resourceName = job.ResourceName;

        // pull docker container logs
        // TODO: find out how we can merge with those for the proxy
        var containers = await dockerClient.Containers.ListContainersAsync(new() { All = true, }, cancellationToken);
        var container = containers.FirstOrDefault(c => c.Names.Contains($"/{resourceName}"));
        if (container is not null)
        {
            var logParams = new Docker.DotNet.Models.ContainerLogsParameters
            {
                ShowStdout = true,
                ShowStderr = true,
                Follow = false,
                Timestamps = false,
            };
            using var stream = await dockerClient.Containers.GetContainerLogsAsync(container.ID, tty: true, logParams, cancellationToken);
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

        return logs;
    }

    internal async Task<DockerImage> GetImageWithDigestAsync(string imageName, bool pull, CancellationToken cancellationToken = default)
    {
        var original = (DockerImage)imageName;
        if (!pull)
        {
            // TODO: we should only handle getting the latest image so that we can use a digest
            return original;
        }

        // pull the image if it does not exist
        var images = await dockerClient.Images.ListImagesAsync(new() { All = true, }, cancellationToken);
        var image = images.FirstOrDefault(i => i.RepoTags?.Contains(imageName) ?? false);
        if (image is null)
        {
            logger.LogInformation("Pulling image: {Image}", imageName);
            var pullParams = new Docker.DotNet.Models.ImagesCreateParameters
            {
                FromImage = imageName,
                Tag = imageName.Split(':').Last(),
            };
            await dockerClient.Images.CreateImageAsync(pullParams, new(), new Progress<Docker.DotNet.Models.JSONMessage>(), cancellationToken);
        }
        else
        {
            // TODO: find a way to check if there is a newer image (by at least a week?) hence pull that one
        }

        // find the image digest and set it in the job
        images = await dockerClient.Images.ListImagesAsync(new() { All = true, }, cancellationToken);
        image = images.Single(i => i.RepoTags?.Contains(imageName) ?? false);
        logger.LogInformation("Using image {Image} at {Digest}", imageName, image.ID);
        return DockerImage.Parse(image.RepoDigests[0]);
    }

    internal enum DockerNetworkName { Server, Proxy, Jobs } // each should be prefixed with "dependabot_" e.g. "dependabot_jobs"
    internal record DockerNetworkRepresentation(string Name, string Id);
    internal async Task<Dictionary<DockerNetworkName, DockerNetworkRepresentation>> GetDockerNetworksAsync(CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<DockerNetworkName, DockerNetworkRepresentation>();
        var networks = await dockerClient.Networks.ListNetworksAsync(cancellationToken: cancellationToken);
        foreach (var value in Enum.GetValues<DockerNetworkName>())
        {
            var name = $"dependabot_{value.ToString().ToLower()}";
            var network = networks.FirstOrDefault(n => n.Name == name)
                ?? throw new InvalidOperationException($"The '{name}' network is missing. Make sure it is setup using compose.");

            results[value] = new DockerNetworkRepresentation(name, network.ID);
        }

        return results;
    }

    internal void DeleteJobDirectory(UpdateJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        // delete the directories associated with the job if they exist
        string[] directories = [
            Path.Join(options.ProxyDirectory, job.Id),
            Path.Join(options.JobsDirectory, job.Id),
        ];

        foreach (var directory in directories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    internal static TargetingContext MakeTargetingContext(Project project, UpdateJob job)
    {
        return new TargetingContext
        {
            Groups =
            [
                $"provider:{project.Type.GetEnumMemberAttrValueOrDefault()}",
                $"project:{project.Id}",
                $"ecosystem:{job.PackageEcosystem}",
            ],
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

public readonly struct UpdaterContext
{
    public UpdaterContext() { }

    public required Project Project { get; init; }
    public required Repository Repository { get; init; }
    public required RepositoryUpdate Update { get; init; }
    public required UpdateJob Job { get; init; }

    public bool UpdatingPullRequest { get; init; } = false;
    public string? UpdateDependencyGroupName { get; init; } = null;
    public List<string> UpdateDependencyNames { get; init; } = [];
}
