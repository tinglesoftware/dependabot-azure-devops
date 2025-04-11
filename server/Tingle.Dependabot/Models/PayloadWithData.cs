using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models;

public class PayloadWithData<T> where T : new()
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? Extensions { get; set; }
}
