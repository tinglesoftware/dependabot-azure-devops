using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotClosePullRequestModel
{
    [JsonPropertyName("dependency-names")]
    public List<string>? DependencyNames { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
