using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Management;

public class Project
{
    [Key, MaxLength(50)]
    public string? Id { get; set; }

    public DateTimeOffset Created { get; set; }

    public DateTimeOffset Updated { get; set; }

    public ProjectType Type { get; set; }

    /// <summary>Name of the project as per provider.</summary>
    public string? Name { get; set; }

    /// <summary>Identifier of the repository as per provider.</summary>
    [Required]
    [JsonIgnore] // only for internal use
    public string? ProviderId { get; set; }

    /// <summary>URL for the project.</summary>
    /// <example>https://dev.azure.com/tingle/dependabot</example>
    [Url]
    [Required]
    public string? Url { get; set; }

    /// <summary>
    /// Token for accessing the project with permissions for repositories, pull requests, and service hooks.
    /// </summary>
    [Required]
    public string? Token { get; set; }

    /// <summary>Whether to set auto complete on created pull requests.</summary>
    public bool AutoComplete { get; set; }

    /// <summary>Identifiers of configs to be ignored in auto complete.</summary>
    public List<int> AutoCompleteIgnoreConfigs { get; set; } = new();

    /// <summary>Merge strategy to use when setting auto complete on created pull requests.</summary>
    public MergeStrategy? AutoCompleteMergeStrategy { get; set; }

    /// <summary>Whether to automatically approve created pull requests.</summary>
    public bool AutoApprove { get; set; }

    /// <summary>Password for Webhooks, ServiceHooks, and Notifications from the provider.</summary>
    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>
    /// Secrets that can be replaced in the registries section of the dependabot configuration file.
    /// </summary>
    [JsonIgnore] // expose this once we know how to protect the values
    public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore] // only for internal use
    public List<Repository> Repositories { get; set; } = new();

    [Timestamp]
    public byte[]? Etag { get; set; }
}

public enum ProjectType
{
    Azure,
}
