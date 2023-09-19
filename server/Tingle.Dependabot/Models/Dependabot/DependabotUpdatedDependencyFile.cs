using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Dependabot;

public class DependabotUpdatedDependencyFile
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("content_encoding")]
    public string? ContentEncoding { get; set; }

    [JsonPropertyName("deleted")]
    public bool? Deleted { get; set; }

    [JsonPropertyName("directory")]
    public string? Directory { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("operation")]
    public string? Operation { get; set; } // convert from string to enum once we know all possible values

    [JsonPropertyName("support_file")]
    public bool? SupportFile { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; } // convert from string to enum once we know all possible values
}
