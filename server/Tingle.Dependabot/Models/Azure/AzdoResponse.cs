using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzdoResponse<T> where T : class
{
    [JsonPropertyName("value")]
    public required T Value { get; set; }

    [JsonPropertyName("count")]
    public required int Count { get; set; }

    public static implicit operator T(AzdoResponse<T> response) => response.Value;
}

public class AzdoProperty
{
    [JsonPropertyName("$type")]
    public required string Type { get; set; }

    [JsonPropertyName("$value")]
    public required string Value { get; set; }
}
