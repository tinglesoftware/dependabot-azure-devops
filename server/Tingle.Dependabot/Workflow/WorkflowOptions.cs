using Microsoft.Extensions.Options;

namespace Tingle.Dependabot.Workflow;

public class WorkflowOptions
{
    /// <summary>URL where subscription notifications shall be sent.</summary>
    public Uri? WebhookEndpoint { get; set; }

    /// <summary>URL on which to access the API from the jobs.</summary>
    public Uri? JobsApiUrl { get; set; }

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
    /// Version of the proxy docker container images to use.
    /// Keeping this value fixed in code is important so that the code that depends on it always works.
    /// More like a dependency.
    /// <br/>
    /// However, in production there maybe an issue that requires a rollback hence the value is placed in options.
    /// </summary>
    public string? ProxyImageTag { get; set; } = "latest";

    /// <summary>
    /// Version of the updater docker container images to use.
    /// Keeping this value fixed in code is important so that the code that depends on it always works.
    /// More like a dependency.
    /// <br/>
    /// However, in production there maybe an issue that requires a rollback hence the value is placed in options.
    /// </summary>
    public string? UpdaterImageTag { get; set; } = "latest";

    /// <summary>
    /// Directory where job artifacts are stored (logs, etc).
    /// The files stored here live as long as the relevant update job lives.
    /// </summary>
    /// <example>/mnt/dependabot/store</example>
    public string? ArtifactsDirectory { get; set; }

    /// <summary>
    /// Directory where certificate files are stored.
    /// </summary>
    /// <example>/mnt/dependabot/certs</example>
    public string? CertsDirectory { get; set; }

    /// <summary>
    /// Directory where proxy config files are written during job scheduling and execution.
    /// This directory is the root for all jobs.
    /// Subdirectories are created for each job and further for each usage type.
    /// For example, if this value is set to <c>/mnt/dependabot/proxy</c>,
    /// A job identified as <c>123456789</c> will have files written at <c>/mnt/dependabot/proxy/123456789</c>
    /// and some nested directories in it such as <c>/mnt/dependabot/proxy/123456789/repo</c>.
    /// </summary>
    /// <example>/mnt/dependabot/proxy</example>
    public string? ProxyDirectory { get; set; }

    /// <summary>
    /// Directory where job files are written during job scheduling and execution.
    /// This directory is the root for all jobs.
    /// Subdirectories are created for each job and further for each usage type.
    /// For example, if this value is set to <c>/mnt/dependabot/jobs</c>,
    /// A job identified as <c>123456789</c> will have files written at <c>/mnt/dependabot/jobs/123456789</c>
    /// and some nested directories in it such as <c>/mnt/dependabot/jobs/123456789/repo</c>.
    /// </summary>
    /// <example>/mnt/dependabot/jobs</example>
    public string? JobsDirectory { get; set; }

    /// <summary>
    /// The default experiments known to be used by the GitHub Dependabot service.
    /// This changes often, update as needed by extracting them from a Dependabot GitHub Action run.
    ///  e.g. https://github.com/tinglesoftware/dependabot-azure-devops/actions/workflows/dependabot/dependabot-updates
    /// <br />
    /// Experiment values are known to be either "true", "false", or a string value.
    /// </summary>
    public IReadOnlyDictionary<string, string> DefaultExperiments { get; } = new Dictionary<string, string>()
    {
        ["record-ecosystem-versions"] = "true",
        ["record-update-job-unknown-error"] = "true",
        ["proxy-cached"] = "true",
        ["move-job-token"] = "true",
        ["dependency-change-validation"] = "true",
        ["nuget-install-dotnet-sdks"] = "true",
        ["nuget-native-analysis"] = "true",
        ["nuget-use-direct-discovery"] = "true",
        ["enable-file-parser-python-local"] = "true",
        ["npm-fallback-version-above-v6"] = "true",
        ["lead-security-dependency"] = "true",
        // NOTE: 'enable-record-ecosystem-meta' is not currently implemented in Dependabot-CLI.
        //       This experiment is primarily for GitHub analytics and doesn't add much value in the DevOps implementation.
        //       See: https://github.com/dependabot/dependabot-core/pull/10905
        // TODO: Revisit this if/when Dependabot-CLI supports it.
        // ["enable-record-ecosystem-meta"] = "true",
        ["enable-shared-helpers-command-timeout"] = "true",
        ["enable-engine-version-detection"] = "true",
        ["avoid-duplicate-updates-package-json"] = "true",
        ["allow-refresh-for-existing-pr-dependencies"] = "true",
        ["enable-bun-ecosystem"] = "true",
        ["exclude-local-composer-packages"] = "true",
    }.AsReadOnly();

    /// <summary>Location/region where to create new update jobs.</summary>
    /// <example>westeurope</example>
    public string? Location { get; set; } // using Azure.Core.Location does not work when binding from IConfiguration

    /// <summary>
    /// Whether we are running in a container.
    /// This is useful to determine how to mount the working directory.
    /// </summary>
    public bool IsInContainer => bool.TryParse(Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"), out var b) && b;
}

internal class WorkflowConfigureOptions : IPostConfigureOptions<WorkflowOptions>, IValidateOptions<WorkflowOptions>
{
    public void PostConfigure(string? name, WorkflowOptions options)
    {
        static string? MakeRooted(string? input)
            => !Path.IsPathRooted(input) && !string.IsNullOrWhiteSpace(input) ? Path.Combine(Directory.GetCurrentDirectory(), input) : input;

        if (!options.IsInContainer)
        {
            options.ArtifactsDirectory = MakeRooted(options.ArtifactsDirectory);
            options.CertsDirectory = MakeRooted(options.CertsDirectory);
            options.ProxyDirectory = MakeRooted(options.ProxyDirectory);
            options.JobsDirectory = MakeRooted(options.JobsDirectory);
        }
    }

    public ValidateOptionsResult Validate(string? name, WorkflowOptions options)
    {
        if (options.JobsApiUrl is null)
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.JobsApiUrl)}' is required");
        }

        if (string.IsNullOrWhiteSpace(options.ProxyImageTag))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.ProxyImageTag)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.UpdaterImageTag))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.UpdaterImageTag)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.ProxyDirectory))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.ProxyDirectory)}' cannot be null or whitespace");
        }

        if (string.IsNullOrWhiteSpace(options.JobsDirectory))
        {
            return ValidateOptionsResult.Fail($"'{nameof(options.JobsDirectory)}' cannot be null or whitespace");
        }

        return ValidateOptionsResult.Success;
    }
}
