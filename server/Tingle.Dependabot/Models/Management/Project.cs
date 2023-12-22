using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Tingle.Dependabot.Workflow;

namespace Tingle.Dependabot.Models.Management;

public class Project
{
    [Key, MaxLength(50)]
    public string? Id { get; set; }

    public DateTimeOffset Created { get; set; }

    public DateTimeOffset Updated { get; set; }

    public ProjectType Type { get; set; }

    /// <summary>Name of the project as per provider.</summary>
    [Required]
    public string? Name { get; set; }

    /// <summary>Description of the project as per provider.</summary>
    public string? Description { get; set; }

    /// <summary>Slug for easy project reference.</summary>
    /// <example>tingle/dependabot</example>
    public string? Slug { get; set; }

    /// <summary>Identifier of the repository as per provider.</summary>
    [Required]
    [JsonIgnore] // only for internal use
    public string? ProviderId { get; set; }

    /// <summary>URL for the project.</summary>
    /// <example>https://dev.azure.com/tingle/dependabot</example>
    [Url]
    public AzureDevOpsProjectUrl Url { get; set; }

    /// <summary>
    /// Token for accessing the project with permissions for repositories, pull requests, and service hooks.
    /// </summary>
    [Required]
    [JsonIgnore] // expose this once we know how to protect the values
    public string? Token { get; set; }

    /// <summary>Whether the project is private.</summary>
    public bool Private { get; set; }

    /// <summary>Auto complete settings.</summary>
    [Required]
    public ProjectAutoComplete AutoComplete { get; set; } = new();

    /// <summary>Auto approve settings.</summary>
    [Required]
    public ProjectAutoApprove AutoApprove { get; set; } = new();

    /// <summary>Password for Webhooks, ServiceHooks, and Notifications from the provider.</summary>
    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>
    /// Secrets that can be replaced in the registries section of the dependabot configuration file.
    /// </summary>
    [JsonIgnore] // expose this once we know how to protect the values
    public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Version of the updater docker container images to use.
    /// If no value is provided, the default version is used.
    /// Providing a value allows to test new feature just for the project.
    /// </summary>
    /// <example>1.20</example>
    public string? UpdaterImageTag { get; set; }

    /// <summary>
    /// Token for accessing GitHub APIs.
    /// If no value is provided, a default token is used.
    /// Providing a value avoids being rate limited in case when there
    /// are many calls at the same time from the same IP.
    /// When provided, it must have <c>read</c> access to public repositories.
    /// </summary>
    /// <example>ghp_1234567890</example>
    [JsonIgnore] // expose this once we know how to protect the values
    public string? GithubToken { get; set; }

    /// <summary>Location/region where to create update jobs.</summary>
    /// <example>westeurope</example>
    public string? Location { get; set; }

    [JsonIgnore] // only for internal use
    public List<Repository> Repositories { get; set; } = [];

    /// <summary>Time at which the synchronization was last done for the project.</summary>
    public DateTimeOffset? Synchronized { get; set; }

    [Timestamp]
    public byte[]? Etag { get; set; }
}

public class ProjectAutoComplete
{
    /// <summary>Whether to set auto complete on created pull requests.</summary>
    public bool Enabled { get; set; }

    /// <summary>Identifiers of configs to be ignored in auto complete.</summary>
    public List<int>? IgnoreConfigs { get; set; }

    /// <summary>Merge strategy to use when setting auto complete on created pull requests.</summary>
    public MergeStrategy? MergeStrategy { get; set; }
}

public class ProjectAutoApprove
{
    /// <summary>Whether to automatically approve created pull requests.</summary>
    public bool Enabled { get; set; }
}

public enum ProjectType
{
    Azure,
}
