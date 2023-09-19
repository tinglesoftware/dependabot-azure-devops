using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotMarkAsProcessedModel
{
    [JsonPropertyName("base-commit-sha")]
    public string? BaseCommitSha { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
