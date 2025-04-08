using Tingle.Dependabot.Models.Management;

namespace Tingle.Dependabot.Workflow;

public class WorkflowOptions
{
    /// <summary>URL where subscription notifications shall be sent.</summary>
    public Uri? WebhookEndpoint { get; set; }

    /// <summary>URL on which to access the API from the jobs.</summary>
    public Uri? JobsApiUrl { get; set; }

    /// <summary>Where new jobs are run/hosted.</summary>
    public UpdateJobPlatform? JobsPlatform { get; set; }

    /// <summary>Resource identifier for the resource group to create jobs in.</summary>
    /// <example>/subscriptions/00000000-0000-1111-0001-000000000000/resourceGroups/DEPENDABOT</example>
    public string? ResourceGroupId { get; set; }

    /// <summary>Name of the file share for the working directory</summary>
    /// <example>/subscriptions/00000000-0000-1111-0001-000000000000/resourceGroups/DEPENDABOT/Microsoft.App/managedEnvironments/dependabot</example>
    public string? AppEnvironmentId { get; set; }

    /// <summary>CustomerId of the LogAnalytics workspace.</summary>
    /// <example>00000000-0000-1111-0001-000000000000</example>
    public string? LogAnalyticsWorkspaceId { get; set; }

    /// <summary>
    /// Version of the updater docker container images to use.
    /// Keeping this value fixed in code is important so that the code that depends on it always works.
    /// More like a dependency.
    /// <br/>
    /// However, in production there maybe an issue that requires a rollback hence the value is placed in options.
    /// </summary>
    /// <example>1.20</example>
    public string? UpdaterImageTag { get; set; }

    /// <summary>
    /// Versions of the updater docker container images to use per ecosystem.
    /// Keeping this value fixed in code is important so that the code that depends on it always works.
    /// If no value is provided for an ecosystem, the default version is used.
    /// </summary>
    /// <example>1.20</example>
    public Dictionary<string, string> UpdaterImageTags { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Root working directory where file are written during job scheduling and execution.
    /// This directory is the root for all jobs.
    /// Subdirectories are created for each job and further for each usage type.
    /// For example, if this value is set to <c>/mnt/dependabot</c>,
    /// A job identified as <c>123456789</c> will have files written at <c>/mnt/dependabot/123456789</c>
    /// and some nested directories in it such as <c>/mnt/dependabot/123456789/repo</c>.
    /// </summary>
    /// <example>/mnt/dependabot</example>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Token for accessing GitHub APIs.
    /// If no value is provided, calls to GitHub are not authenticated.
    /// Providing a value avoids being rate limited in case when there
    /// are many upgrades at the same time from the same IP.
    /// When provided, it must have <c>read</c> access to public repositories.
    /// </summary>
    /// <example>ghp_1234567890</example>
    public string? GithubToken { get; set; }

    /// <summary>Location/region where to create new update jobs.</summary>
    /// <example>westeurope</example>
    public string? Location { get; set; } // using Azure.Core.Location does not work when binding from IConfiguration

    /// <summary>
    /// Name of the docker network to use for the jobs.
    /// This is only used when the the target platform is <see cref="UpdateJobPlatform.DockerCompose"/>.
    /// </summary>
    /// <example>dependabot_jobs</example>
    public string? DockerNetwork { get; set; }

    public string GetUpdaterImageTag(string ecosystem, Models.Management.Project project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ecosystem);

        if (UpdaterImageTags.TryGetValue(ecosystem, out var tag)) return tag;

        return project.UpdaterImageTag ?? UpdaterImageTag!;
    }
}
