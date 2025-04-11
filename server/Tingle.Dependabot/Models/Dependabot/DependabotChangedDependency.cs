using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotChangedDependency
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("previous-version")]
    public string? PreviousVersion { get; set; }

    [JsonPropertyName("requirements")]
    public JsonArray? Requirements { get; set; }

    [JsonPropertyName("previous-requirements")]
    public JsonArray? PreviousRequirements { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("directory")]
    public string? Directory { get; set; }

    [JsonPropertyName("removed")]
    public bool? Removed { get; set; }
}
