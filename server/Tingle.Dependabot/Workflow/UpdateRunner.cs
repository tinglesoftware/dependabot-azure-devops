using System.Formats.Tar;
using Docker.DotNet;
using Microsoft.Extensions.Options;
using Tingle.Dependabot.Models;
using Tingle.Dependabot.Models.Management;
using Tingle.Extensions.Primitives;

namespace Tingle.Dependabot.Workflow;

public interface IUpdateRunner
{
    Task RunAsync(UpdaterContext context, CancellationToken cancellationToken = default);
}

internal partial class UpdateRunner(IConfigFilesWriter configFilesWriter,
                                    IOptions<WorkflowOptions> optionsAccessor,
                                    ILogger<UpdateRunner> logger) : IUpdateRunner
{
    private const string VolumeNameCerts = "certs";
    private const string VolumeNameJobs = "jobs";
    private const string VolumeNameProxy = "proxy";
    private const string MountPathJobs = "/mnt/dependabot/jobs";
    private const string MountPathCert = "/usr/local/share/ca-certificates/dbot-ca.crt";
    private const int ProxyPort = 1080;

    private readonly WorkflowOptions options = optionsAccessor?.Value ?? throw new ArgumentNullException(nameof(optionsAccessor));
    private readonly ILogger logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly DockerClient dockerClient = new DockerClientConfiguration().CreateClient();

    public async Task RunAsync(UpdaterContext context, CancellationToken cancellationToken = default)
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

        // prepare credentials and job directory
        var credentials = configFilesWriter.MakeCredentials(context);

        // prepare directories and path for artifacts
        var artifactsDirectory = Path.Join(options.ArtifactsDirectory, job.Id);
        if (!Directory.Exists(artifactsDirectory)) Directory.CreateDirectory(artifactsDirectory);
        var logsPath = job.LogsPath = Path.Join(artifactsDirectory, "logs.txt");
        var flameGraphPath = job.FlameGraphPath = Path.Join(artifactsDirectory, "flamegraph.html");
        if (File.Exists(logsPath)) File.Delete(logsPath);
        if (File.Exists(flameGraphPath)) File.Delete(flameGraphPath);
        var logsStream = File.Open(logsPath, FileMode.OpenOrCreate);
        using var flameGraphStream = File.Open(flameGraphPath, FileMode.OpenOrCreate);

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
        var mountedProxyConfigSubPath = Path.GetRelativePath(options.ProxyDirectory, proxyConfigPath);

        // write job definition file
        var jobDirectory = Path.Join(options.JobsDirectory, job.Id);
        if (!Directory.Exists(jobDirectory)) Directory.CreateDirectory(jobDirectory);
        var jobDefinitionPath = Path.Join(jobDirectory, "job.json");
        if (File.Exists(jobDefinitionPath)) File.Delete(jobDefinitionPath);
        var outputPath = Path.Join(jobDirectory, "output.json");
        if (File.Exists(outputPath)) File.Delete(outputPath);

        var jobConfigContext = new JobConfigContext(context, credentials);
        await configFilesWriter.WriteJobAsync(jobDefinitionPath, jobConfigContext, cancellationToken);
        var caCertPath = Path.Join(options.CertsDirectory, "cert.crt");

        // the job path we have might local to the machine but we need to be based on the mount we have in the container
        // example: change /Users/maxwell/Documents/dependabot-azure-devops/server/Tingle.Dependabot/work/jobs/1359497145993567115/job.json
        //              to /mnt/dependabot/jobs/1359497145993567115/job.json
        // example: change /Users/maxwell/Documents/dependabot-azure-devops/server/Tingle.Dependabot/work/jobs/1359497145993567115/output.json
        //              to /mnt/dependabot/jobs/1359497145993567115/output.json
        var mountedJobDefinitionPath = Path.Combine(MountPathJobs, Path.GetRelativePath(options.JobsDirectory, jobDefinitionPath));
        var mountedOutputPath = Path.Combine(MountPathJobs, Path.GetRelativePath(options.JobsDirectory, outputPath));

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
            ["DEPENDABOT_DEBUG"] = project.Debug.ToString().ToLower(),
            ["FLAMEGRAPH"] = "1",
        };
        var updateEnvNamesForProxyUrl = new List<string> { "http_proxy", "HTTP_PROXY", "https_proxy", "HTTPS_PROXY" };

        // create temporary networks which we can delete later
        var internetNetwork = await CreateDockerNetworkAsync(@internal: false, cancellationToken);
        var noInternetNetwork = await CreateDockerNetworkAsync(@internal: true, cancellationToken);

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
                    [internetNetwork.Name] = new() { NetworkID = internetNetwork.Id, },
                    [noInternetNetwork.Name] = new() { NetworkID = noInternetNetwork.Id, },
                }
            },
            Entrypoint = proxyEntrypoint,
            Env = [.. proxyEnv.Select(kvp => $"{kvp.Key}={kvp.Value}")],
        };

        // create the proxy container
        var proxyContainer = await dockerClient.Containers.CreateContainerAsync(proxyContainerParams, cancellationToken);
        logger.CreatedProxyContainer(job.Id, proxyContainer.ID);

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

        // start the proxy container with streaming logs
        await dockerClient.Containers.StartContainerAsync(proxyContainer.ID, new(), cancellationToken);
        var proxyLogsTask = StreamLogsAsync(proxyContainer.ID, "  proxy | ", logsStream, cancellationToken);
        logger.StartedProxyContainer(job.Id);

        // find the proxy URL
        var inspection = await dockerClient.Containers.InspectContainerAsync(proxyContainer.ID, cancellationToken);
        var proxyUrl = $"http://{inspection.NetworkSettings.Networks[noInternetNetwork.Name].IPAddress}:{ProxyPort}";

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
                    [noInternetNetwork.Name] = new() { NetworkID = noInternetNetwork.Id, }
                }
            },
            Cmd = updaterCommand,
            Env = [.. updaterEnv.Select(kvp => $"{kvp.Key}={kvp.Value}")],
        };
        foreach (var name in updateEnvNamesForProxyUrl) updaterContainerParams.Env.Add($"{name}={proxyUrl}");

        // create the updater container
        var updaterContainer = await dockerClient.Containers.CreateContainerAsync(updaterContainerParams, cancellationToken);
        logger.CreatedUpdaterContainer(job.Id, updaterContainer.ID);

        // start the updater container with streaming logs
        await dockerClient.Containers.StartContainerAsync(updaterContainer.ID, new(), cancellationToken);
        var updaterLogsTask = StreamLogsAsync(updaterContainer.ID, "updater | ", logsStream, cancellationToken);
        logger.StartedUpdaterContainer(job.Id);

        // wait for the updater container to exit, then stop the proxy (if not stopped, logs will not complete collection)
        await dockerClient.Containers.WaitContainerAsync(updaterContainer.ID, cancellationToken);
        await dockerClient.Containers.StopContainerAsync(proxyContainer.ID, new() { WaitBeforeKillSeconds = 0 }, cancellationToken);

        // collect the flamegraph
        await ReadFlameGraphAsync(updaterContainer.ID, flameGraphStream, cancellationToken);
        await flameGraphStream.FlushAsync(cancellationToken);

        // complete the collection of logs
        await Task.WhenAll(proxyLogsTask, updaterLogsTask);
        await logsStream.FlushAsync(cancellationToken);

        // update the job
        inspection = await dockerClient.Containers.InspectContainerAsync(updaterContainer.ID, cancellationToken);
        job.Status = inspection.State.ExitCode is 0 ? UpdateJobStatus.Succeeded : UpdateJobStatus.Failed;
        job.Start = DateTimeOffset.Parse(inspection.State.StartedAt);
        job.End = DateTimeOffset.Parse(inspection.State.FinishedAt);
        job.Duration = Convert.ToInt64(Math.Ceiling((job.End - job.Start).Value.TotalMilliseconds));
        if (update.LatestJobId == job.Id) update.LatestJobStatus = job.Status;

        // delete the containers
        await dockerClient.Containers.RemoveContainerAsync(updaterContainer.ID, new() { Force = true, }, cancellationToken);
        await dockerClient.Containers.RemoveContainerAsync(proxyContainer.ID, new() { Force = true, }, cancellationToken);

        // delete created directories
        if (Directory.Exists(jobDirectory)) Directory.Delete(jobDirectory, recursive: true);
        if (Directory.Exists(proxyDirectory)) Directory.Delete(proxyDirectory, recursive: true);

        // delete the networks created
        await dockerClient.Networks.DeleteNetworkAsync(noInternetNetwork.Id, cancellationToken);
        await dockerClient.Networks.DeleteNetworkAsync(internetNetwork.Id, cancellationToken);
    }

    internal async Task StreamLogsAsync(string containerId, string prefix, Stream destination, CancellationToken cancellationToken = default)
    {
        try
        {
#pragma warning disable CS0618 // Type or member is obsolete
            using var logStream = await dockerClient.Containers.GetContainerLogsAsync(
                containerId,
                new Docker.DotNet.Models.ContainerLogsParameters
                {
                    ShowStdout = true,
                    ShowStderr = true,
                    Follow = true,
                    Timestamps = false,
                },
                cancellationToken
            );
#pragma warning restore CS0618 // Type or member is obsolete

            using var reader = new StreamReader(logStream);
            using var writer = new StreamWriter(destination, leaveOpen: true) { AutoFlush = true };
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                await writer.WriteLineAsync($"{prefix}{line}");
            }

        }
        catch (OperationCanceledException)
        {
            // do nothing, we stopped everything
        }
    }
    internal async Task ReadFlameGraphAsync(string containerId, Stream destination, CancellationToken cancellationToken = default)
    {
        const string fileName = "dependabot-flamegraph.html";
        var @params = new Docker.DotNet.Models.GetArchiveFromContainerParameters { Path = $"/tmp/{fileName}" };
        try
        {
            var response = await dockerClient.Containers.GetArchiveFromContainerAsync(containerId, @params, false, cancellationToken);
            using var reader = new TarReader(response.Stream, leaveOpen: true);
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) is not null)
            {
                if (entry.EntryType == TarEntryType.RegularFile &&
                    entry.Name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    // Copy file contents directly to the target stream
                    await entry.DataStream!.CopyToAsync(destination, cancellationToken);
                    return;
                }
            }
            throw new FileNotFoundException($"'/tmp/{fileName}' not found in container archive.");
        }
        catch (DockerContainerNotFoundException) // happens when the container did not succeed
        {
            return;
        }
        catch (EndOfStreamException) // for some reason it is thrown after the file is already copied
        {
            // Safely ignore if the file was already found and copied
            if (destination.Length > 0)
                return;

            throw new IOException("Unexpected end of stream while reading archive.");
        }
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
            logger.PullImage(imageName);
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
        logger.UsingImage(imageName, image.ID);
        return DockerImage.Parse(image.RepoDigests[0]);
    }

    internal record DockerNetworkRepresentation(string Name, string Id);
    internal async Task<DockerNetworkRepresentation> CreateDockerNetworkAsync(bool @internal, CancellationToken cancellationToken = default)
    {
        var parameters = new Docker.DotNet.Models.NetworksCreateParameters
        {
            Name = Keygen.Create(10).ToLowerInvariant(),
            Driver = "bridge",
            Internal = @internal,
        };
        var network = await dockerClient.Networks.CreateNetworkAsync(parameters, cancellationToken);
        var inspection = await dockerClient.Networks.InspectNetworkAsync(network.ID, cancellationToken);
        return new DockerNetworkRepresentation(inspection.Name, network.ID);
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
    public required Project Project { get; init; }
    public required Repository Repository { get; init; }
    public required RepositoryUpdate Update { get; init; }
    public required UpdateJob Job { get; init; }

    public required IReadOnlyDictionary<string, Models.Azure.PullRequestProperties> ExistingPullRequests { get; init; }
    public required IReadOnlyList<Models.Dependabot.DependabotSecurityAdvisory> SecurityAdvisories { get; init; }

    public required bool UpdatingPullRequest { get; init; }
    public required string? DependencyGroupToRefresh { get; init; }
    public required List<string> DependencyNamesToUpdate { get; init; }
}
