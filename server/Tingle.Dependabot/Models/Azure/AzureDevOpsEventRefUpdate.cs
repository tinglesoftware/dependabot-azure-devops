using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzureDevOpsEventRefUpdate
{
    [Required]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("oldObjectId")]
    public string? OldObjectId { get; set; }

    [JsonPropertyName("newObjectId")]
    public string? NewObjectId { get; set; }
}
