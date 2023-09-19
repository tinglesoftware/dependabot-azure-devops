using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzureDevOpsEventRepository
{
    /// <summary>
    /// The unique identifier of the repository.
    /// </summary>
    [Required]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// The name of the repository.
    /// </summary>
    [Required]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The details about the project which owns the repository.
    /// </summary>
    [Required]
    [JsonPropertyName("project")]
    public AzureDevOpsEventRepositoryProject? Project { get; set; }

    /// <summary>
    /// The default branch of the repository.
    /// </summary>
    [JsonPropertyName("defaultBranch")]
    public string? DefaultBranch { get; set; } // should not be required because some repositories do not have default branches

    [Required]
    [JsonPropertyName("remoteUrl")]
    public string? RemoteUrl { get; set; }
}
