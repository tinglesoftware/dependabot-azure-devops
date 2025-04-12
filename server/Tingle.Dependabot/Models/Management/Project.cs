using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Tingle.Dependabot.Workflow;
using Tingle.Extensions.Primitives;

namespace Tingle.Dependabot.Models.Management;

public class Project
{
    [Key, MaxLength(50)]
    public required string Id { get; set; }

    public DateTimeOffset Created { get; set; }

    public DateTimeOffset Updated { get; set; }

    public ProjectType Type { get; set; }

    /// <summary>Name of the project as per provider.</summary>
    public required string Name { get; set; }

    /// <summary>Description of the project as per provider.</summary>
    public string? Description { get; set; }

    /// <summary>Slug for easy project reference.</summary>
    /// <example>tingle/dependabot</example>
    public string? Slug { get; set; }

    /// <summary>Identifier of the repository as per provider.</summary>
    [JsonIgnore] // only for internal use
    public required string ProviderId { get; set; }

    /// <summary>URL for the project.</summary>
    /// <example>https://dev.azure.com/tingle/dependabot</example>
    [Url]
    public AzureDevOpsProjectUrl Url { get; set; }

    /// <summary>
    /// Token for accessing the project with permissions for repositories, pull requests, and service hooks.
    /// </summary>
    [JsonIgnore] // expose this once we know how to protect the values
    public required string Token { get; set; }

    /// <summary>
    /// User identifier for the provided token.
    /// </summary>
    [JsonIgnore] // only for internal use
    public required string UserId { get; set; }

    /// <summary>Whether the project is private.</summary>
    public bool Private { get; set; }

    /// <summary>Auto complete settings.</summary>
    public required ProjectAutoComplete AutoComplete { get; set; }

    /// <summary>Auto approve settings.</summary>
    public required ProjectAutoApprove AutoApprove { get; set; }

    /// <summary>Password for Webhooks, ServiceHooks, and Notifications from the provider.</summary>
    [DataType(DataType.Password)]
    public required string Password { get; set; }

    /// <summary>
    /// Secrets that can be replaced in the registries section of the dependabot configuration file.
    /// </summary>
    [JsonIgnore] // expose this once we know how to protect the values
    public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Experiments for the project. If none are provided, the default ones are used
    /// </summary>
    public Dictionary<string, string>? Experiments { get; set; }

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
    public Etag? Etag { get; set; } // TODO: remove nullability once we reset the migrations
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
