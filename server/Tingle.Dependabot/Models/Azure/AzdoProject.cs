using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Azure;

public class AzdoProject : AzdoProjectReference
{
    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("visibility")]
    public required AzdoProjectVisibility Visibility { get; set; }

    [JsonPropertyName("lastUpdateTime")]
    public required DateTimeOffset LastUpdatedTime { get; set; }
}

public class AzdoProjectReference
{
    /// <summary>The unique identifier of the project.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>The name of the project.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>The URL for the project.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

[JsonConverter(typeof(JsonStringEnumMemberConverter<AzdoProjectVisibility>))]
public enum AzdoProjectVisibility
{
    [EnumMember(Value = "private")] Private,
    [EnumMember(Value = "organization")] Organization,
    [EnumMember(Value = "public")] Public,
    [EnumMember(Value = "systemPrivate")] SystemPrivate,
}
