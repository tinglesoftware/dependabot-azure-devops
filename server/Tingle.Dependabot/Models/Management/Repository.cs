using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Tingle.Dependabot.Models.Dependabot;

namespace Tingle.Dependabot.Models.Management;

public class Repository
{
    [Key, MaxLength(50)]
    public required string Id { get; set; }

    public DateTimeOffset Created { get; set; }

    public DateTimeOffset Updated { get; set; }

    /// <summary>Identifier of the project.</summary>
    [JsonIgnore] // only for internal use
    public string ProjectId { get; set; } = default!; // marking required does not play well with JsonIgnore

    /// <summary>Name of the repository as per provider.</summary>
    public string? Name { get; set; }

    /// <summary>Slug for easy repository reference.</summary>
    /// <example>tingle/dependabot/_git/repro-747</example>
    public string? Slug { get; set; }

    /// <summary>Identifier of the repository as per provider.</summary>
    [JsonIgnore] // only for internal use
    public string ProviderId { get; set; } = default!; // marking required does not play well with JsonIgnore

    /// <summary>
    /// Latest commit SHA synchronized for the configuration file.
    /// </summary>
    [MaxLength(200)]
    public string? LatestCommit { get; set; }

    /// <summary>Contents of the configuration file as of <see cref="LatestCommit"/>.</summary>
    [Required]
    [JsonIgnore] // only for internal use
    public string? ConfigFileContents { get; set; }

    /// <summary>
    /// Exception that encountered, if any, when parsing the configuration file.
    /// This is populated when <c>updates</c> is <c>null</c> or empty.
    /// </summary>
    public string? SyncException { get; set; }

    /// <summary>
    /// Updates for the repository, extracted from the configuration file.
    /// When <c>null</c> or empty, there was a parsing exception.
    /// </summary>
    public List<RepositoryUpdate> Updates { get; set; } = [];

    /// <summary>
    /// Registries for the repository, extracted from the configuration file.
    /// When <c>null</c> or empty, there was a parsing exception.
    /// </summary>
    [JsonIgnore] // only for internal use
    public Dictionary<string, DependabotRegistry> Registries { get; set; } = [];

    public RepositoryUpdate? GetUpdate(UpdateJob job)
    {
        // find the update (we assume that there is only one matching the ecosystem and directory/directories)
        return (from u in Updates
                where u.PackageEcosystem == job.PackageEcosystem
                where u.Directory == job.Directory
                where u.Directories == job.Directories
                select u).SingleOrDefault();
    }
}
