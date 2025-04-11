using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotCreatePullRequestModel
{
    [JsonPropertyName("dependencies")]
    public List<DependabotChangedDependency>? Dependencies { get; set; }

    [JsonPropertyName("updated-dependency-files")]
    public List<DependabotUpdatedDependencyFile>? DependencyFiles { get; set; }

    [JsonPropertyName("base-commit-sha")]
    public string? BaseCommitSha { get; set; }

    [JsonPropertyName("commit-message")]
    public string? CommitMessage { get; set; }

    [JsonPropertyName("pr-title")]
    public string? PrTitle { get; set; }

    [JsonPropertyName("pr-body")]
    public string? PrBody { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
