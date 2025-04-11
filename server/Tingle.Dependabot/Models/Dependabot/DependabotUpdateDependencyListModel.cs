using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotUpdateDependencyListModel
{
    [JsonPropertyName("dependencies")]
    public JsonArray? Dependencies { get; set; }

    [JsonPropertyName("dependency_files")]
    public List<string>? DependencyFiles { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
