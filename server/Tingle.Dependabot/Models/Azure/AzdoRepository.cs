using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzdoRepository
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("project")]
    public required AzdoProject Project { get; set; }

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonPropertyName("isFork")]
    public bool IsFork { get; set; }
}

public class AzdoListResponse<T> where T : class
{
    [JsonPropertyName("value")]
    public required List<T> Value { get; set; }

    [JsonPropertyName("count")]
    public required int Count { get; set; }
}
