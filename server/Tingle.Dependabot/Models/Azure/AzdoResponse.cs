using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public record AzdoResponse<T>(
    [property: JsonPropertyName("value")] T Value,
    [property: JsonPropertyName("count")] int Count) where T : class
{
    public static implicit operator T(AzdoResponse<T> response) => response.Value;
}

public record AzdoProperty(
    [property: JsonPropertyName("$type")] string Type,
    [property: JsonPropertyName("$value")] string Value);
