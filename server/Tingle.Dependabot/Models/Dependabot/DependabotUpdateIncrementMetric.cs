using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotUpdateIncrementMetric
{
    [JsonPropertyName("metric")]
    public string? Metric { get; set; }

    [JsonPropertyName("tags")]
    public Dictionary<string, object>? Tags { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}

public class DependabotUpdateRecordEcosystemVersions
{
    [JsonPropertyName("ecosystem_versions")]
    public Dictionary<string, object>? EcosystemVersions { get; set; }
}
