using Tingle.Dependabot.Models;

namespace Tingle.Dependabot.Workflow;

public class WorkflowOptions
{
    /// <summary>Whether to synchronize repositories on startup.</summary>
    public bool SynchronizeOnStartup { get; set; } = true;

    /// <summary>Whether to load schedules on startup.</summary>
    public bool LoadSchedulesOnStartup { get; set; } = true;

    /// <summary>Whether to create/update notifications on startup.</summary>
    public bool CreateOrUpdateWebhooksOnStartup { get; set; } = true;

    /// <summary>URL where subscription notifications shall be sent.</summary>
    public Uri? WebhookEndpoint { get; set; }

    /// <summary>Password used for creation of subscription and authenticating incoming notifications.</summary>
    public string? SubscriptionPassword { get; set; }

    /// <summary>Resource identifier for the resource group to create jobs in.</summary>
    /// <example>/subscriptions/00000000-0000-1111-0001-000000000000/resourceGroups/DEPENDABOT</example>
    public string? ResourceGroupId { get; set; }

    /// <summary>CustomerId of the LogAnalytics workspace.</summary>
    /// <example>00000000-0000-1111-0001-000000000000</example>
    public string? LogAnalyticsWorkspaceId { get; set; }

    /// <summary>AuthenticationKey of the LogAnalytics workspace.</summary>
    /// <example>AAAAAAAAAAA=</example>
    public string? LogAnalyticsWorkspaceKey { get; set; }

    /// <summary>Resource identifier for the managed identity used to pull container images.</summary>
    /// <example>/subscriptions/00000000-0000-1111-0001-000000000000/resourceGroups/DEPENDABOT/providers/Microsoft.ManagedIdentity/userAssignedIdentities/dependabot</example>
    public string? ManagedIdentityId { get; set; }

    /// <summary>
    /// Template representing the docker container image  to use.
    /// Keeping this value fixed in code is important so that the code that depends on it always works.
    /// More like a dependency.
    /// <br/>
    /// However, in production there maybe an issue that requires a rollback hence the value is placed in options.
    /// </summary>
    /// <example>ghcr.io/tinglesoftware/dependabot-updater-{{ecosystem}}:1.20</example>
    public string? UpdaterContainerImageTemplate { get; set; }

    /// <summary>URL for the project.</summary>
    public AzureDevOpsProjectUrl? ProjectUrl { get; set; }

    /// <summary>Authentication token for accessing the project.</summary>
    public string? ProjectToken { get; set; }

    /// <summary>Whether to debug all jobs.</summary>
    public bool? DebugJobs { get; set; }

    /// <summary>URL on which to access the API from the jobs.</summary>
    /// <example>https://dependabot.dummy-123.westeurope.azurecontainerapps.io</example>
    public string? JobsApiUrl { get; set; }

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

    /// <summary>Whether updates should be created in the same order.</summary>
    public bool? DeterministicUpdates { get; set; }

    /// <summary>Whether update jobs should fail when an exception occurs.</summary>
    public bool FailOnException { get; set; }

    /// <summary>Whether to set automatic completion of pull requests.</summary>
    public bool? AutoComplete { get; set; }

    public string? AutoCompleteIgnoreConfigs { get; set; }

    public MergeStrategy? AutoCompleteMergeStrategy { get; set; }

    /// <summary>Whether to automatically approve pull requests.</summary>
    public bool? AutoApprove { get; set; }

    /// <summary>
    /// Token for accessing GitHub APIs.
    /// If no value is provided, calls to GitHub are not authenticated.
    /// Providing a value avoids being rate limited in case when there
    /// are many upgrades at the same time from the same IP.
    /// When provided, it must have <c>read</c> access to public repositories.
    /// </summary>
    /// <example>ghp_1234567890</example>
    public string? GithubToken { get; set; }

    /// <summary>
    /// Secrets that can be replaced in the registries section of the configuration file.
    /// </summary>
    public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Location/region where to create new update jobs.</summary>
    public string? Location { get; set; } // using Azure.Core.Location does not work when binding from IConfiguration

    /// <summary>Name of the storage account.</summary>
    /// <example>dependabot-1234567890</example>
    public string? StorageAccountName { get; set; } // only used with ContainerInstances

    /// <summary>Access key for the storage account.</summary>
    public string? StorageAccountKey { get; set; } // only used with ContainerInstances
    
    /// <summary>Name of the file share for the working directory</summary>
    /// <example>working-dir</example>
    public string? FileShareName { get; set; } // only used with ContainerInstances
    
    /// <summary>
    /// Possible/allowed paths for the configuration files in a repository.
    /// </summary>
    public IReadOnlyList<string> ConfigurationFilePaths { get; set; } = new[] {
        // TODO: restore checks in .azuredevops folder once either the code can check that folder or we are passing ignore conditions via update_jobs API
        //".azuredevops/dependabot.yml",
        //".azuredevops/dependabot.yaml",

        ".github/dependabot.yml",
        ".github/dependabot.yaml",
    };
}
