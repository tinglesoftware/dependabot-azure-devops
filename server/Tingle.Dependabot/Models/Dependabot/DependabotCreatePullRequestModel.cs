using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotCreatePullRequestModel
{
    [Required]
    [MinLength(1)]
    [JsonPropertyName("dependencies")]
    public List<DependabotChangedDependency>? Dependencies { get; set; }

    [Required]
    [MinLength(1)]
    [JsonPropertyName("updated-dependency-files")]
    public List<DependabotUpdatedDependencyFile>? DependencyFiles { get; set; }

    [JsonPropertyName("base-commit-sha")]
    public string? BaseCommitSha { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
