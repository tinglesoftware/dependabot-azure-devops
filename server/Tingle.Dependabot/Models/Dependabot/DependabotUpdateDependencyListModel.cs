using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotUpdateDependencyListModel
{
    [Required]
    [JsonPropertyName("dependencies")]
    public JsonArray? Dependencies { get; set; }

    [Required]
    [MinLength(1)]
    [JsonPropertyName("dependency_files")]
    public List<string>? DependencyFiles { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
