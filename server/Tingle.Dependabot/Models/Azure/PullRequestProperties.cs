using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public record PullRequestProperties(
    string? PackageManager,
    PullRequestStoredDependencies Dependencies);

public record PullRequestStoredDependency(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("directory")] string? Directory,
    [property: JsonPropertyName("removed")] bool? Removed)
{
    public static implicit operator Dependabot.DependabotExistingPR(PullRequestStoredDependency dep) => new(dep.Name, dep.Version, dep.Directory);
    public static implicit operator PullRequestStoredDependency(Dependabot.DependabotDependency dep) => new(dep.Name, dep.Version, dep.Directory, dep.Removed);
}

public record PullRequestStoredDependencies(
    [property: JsonPropertyName("dependencies")] List<PullRequestStoredDependency> Dependencies,
    [property: JsonPropertyName("dependency-group-name")] string? DependencyGroupName = null);
