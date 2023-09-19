using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotClosePullRequestModel
{
    //[Required]
    //[MinLength(1)]
    //[JsonPropertyName("dependency-names")]
    //public List<string>? DependencyNames { get; set; } // This can also be a string that's why it has not been enabled

    [JsonPropertyName("reason")]
    public string? Reason { get; set; } // convert from string to enum once we know all possible values

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
