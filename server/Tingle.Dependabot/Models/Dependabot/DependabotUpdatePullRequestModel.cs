using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotUpdatePullRequestModel
{
    [JsonPropertyName("dependency-names")]
    public List<string>? DependencyNames { get; set; }

    [JsonPropertyName("updated-dependency-files")]
    public List<DependabotUpdatedDependencyFile>? DependencyFiles { get; set; }

    [JsonPropertyName("base-commit-sha")]
    public string? BaseCommitSha { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
