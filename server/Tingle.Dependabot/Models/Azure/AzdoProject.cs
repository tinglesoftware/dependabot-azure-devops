using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzdoProject
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("visibility")]
    public required AzdoProjectVisibility Visibility { get; set; }

    [JsonPropertyName("lastUpdateTime")]
    public required DateTimeOffset LastUpdatedTime { get; set; }
}

public class AzdoGitRepository
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("project")]
    public required AzdoProject Project { get; set; }

    [JsonPropertyName("isDisabled")]
    public required bool IsDisabled { get; set; }

    [JsonPropertyName("isFork")]
    public required bool IsFork { get; set; }
}
