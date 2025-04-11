using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotRecordUpdateJobErrorModel
{
    [JsonPropertyName("error-type")]
    public string? ErrorType { get; set; }

    [JsonPropertyName("error-detail")]
    public JsonNode? ErrorDetail { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
