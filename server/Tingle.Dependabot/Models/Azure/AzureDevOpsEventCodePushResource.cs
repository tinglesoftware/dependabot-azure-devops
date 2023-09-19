using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzureDevOpsEventCodePushResource
{
    /// <summary>
    /// List of updated references.
    /// </summary>
    [Required]
    [JsonPropertyName("refUpdates")]
    public List<AzureDevOpsEventRefUpdate>? RefUpdates { get; set; }

    /// <summary>
    /// Details about the repository.
    /// </summary>
    [Required]
    [JsonPropertyName("repository")]
    public AzureDevOpsEventRepository? Repository { get; set; }
}
