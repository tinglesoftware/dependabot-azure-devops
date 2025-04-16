using System.Text.Json.Serialization;
using Tingle.Dependabot.Models.Dependabot;

namespace Tingle.Dependabot.Models.Management;

public record RepositoryUpdate : DependabotUpdate
{
    public RepositoryUpdate() { } // required for deserialization

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public RepositoryUpdate(DependabotUpdate update) : base(update) { }

    /// <summary>The dependency files.</summary>
    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = [];

    /// <summary>Identifier of the latest job.</summary>
    [JsonPropertyName("latest-job-id")]
    public string? LatestJobId { get; set; }

    /// <summary>Status of the latest job.</summary>
    [JsonPropertyName("latest-job-status")]
    public UpdateJobStatus? LatestJobStatus { get; set; }

    /// <summary>Time at which the latest update was run.</summary>
    [JsonPropertyName("latest-update")]
    public DateTimeOffset? LatestUpdate { get; set; }
}
