using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzureDevOpsEventRepositoryProject
{
    /// <summary>
    /// The unique identifier of the project.
    /// </summary>
    [Required]
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// The name of the project.
    /// </summary>
    [Required]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>
    /// The URL for the project.
    /// </summary>
    [Required]
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
