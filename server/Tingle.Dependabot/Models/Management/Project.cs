using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Management;

public class Project : IProtectable
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
    public string ProviderId { get; set; } = default!; // marking required does not play well with JsonIgnore

    /// <summary>URL for the project.</summary>
    /// <example>https://dev.azure.com/tingle/dependabot</example>
    [Url]
    public AzureDevOpsProjectUrl Url { get; set; }

    /// <summary>
    /// Token for accessing the project with permissions for repositories, pull requests, and service hooks.
    /// </summary>
    public required string Token { get; set; }

    /// <summary>
    /// User identifier for the provided token.
    /// </summary>
    [JsonIgnore] // only for internal use
    public string UserId { get; set; } = default!; // marking required does not play well with JsonIgnore

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
    public Dictionary<string, string> Secrets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Experiments for the project. If none are provided, the default ones are used
    /// </summary>
    public Dictionary<string, string>? Experiments { get; set; }

    /// <summary>
    /// Token for accessing GitHub APIs.
    /// This is necessary for getting changelogs, commits, etc for dependencies.
    /// Providing a value avoids being rate limited in case when there
    /// are many calls at the same time from the same IP.
    /// When provided, it must have <c>read</c> access to public repositories.
    /// </summary>
    /// <example>ghp_1234567890</example>
    public string? GithubToken { get; set; }

    /// <summary>
    /// Whether to enable debug on jobs run for the project.
    /// Defaults to <see langword="true"/>.
    /// </summary>
    public bool Debug { get; set; } = true;

    /// <summary>Time at which the synchronization was last done for the project.</summary>
    public DateTimeOffset? Synchronized { get; set; }

    /// <inheritdoc/>
    public void Protect()
    {
        Token = Token.Protect();
        GithubToken = GithubToken?.Protect();
        Password = Password.Protect();
        Secrets = Secrets.ToDictionary(p => p.Key, p => p.Key.Protect());
    }
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

[JsonConverter(typeof(JsonStringEnumMemberConverter<ProjectType>))]
public enum ProjectType
{
    [EnumMember(Value = "azure")]
    Azure,
}

[JsonConverter(typeof(JsonStringEnumMemberConverter<MergeStrategy>))]
public enum MergeStrategy
{
    [EnumMember(Value = "noFastForward")] NoFastForward = 0,
    [EnumMember(Value = "rebase")] Rebase = 1,
    [EnumMember(Value = "rebaseMerge")] RebaseMerge = 2,
    [EnumMember(Value = "squash")] Squash = 3,
}
