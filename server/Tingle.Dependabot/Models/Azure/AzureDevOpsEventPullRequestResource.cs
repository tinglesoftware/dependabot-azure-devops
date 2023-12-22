using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzureDevOpsEventPullRequestResource
{
    /// <summary>
    /// Details about the repository.
    /// </summary>
    [Required]
    [JsonPropertyName("repository")]
    public AzureDevOpsEventRepository? Repository { get; set; }

    /// <summary>
    /// The identifier of the Pull Request.
    /// </summary>
    [Required]
    [JsonPropertyName("pullRequestId")]
    public int PullRequestId { get; set; }

    /// <summary>
    /// The status of the Pull Request.
    /// </summary>
    [Required]
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// The title of the Pull Request.
    /// </summary>
    [Required]
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// The branch of the repository from which the changes are picked from in the Pull Request.
    /// </summary>
    /// <example>refs/heads/feature/my-feature</example>
    [Required]
    [JsonPropertyName("sourceRefName")]
    public string? SourceRefName { get; set; }

    /// <summary>
    /// The branch of the repository to which the merge shall be done.
    /// </summary>
    /// <example>refs/heads/main</example>
    [Required]
    [JsonPropertyName("targetRefName")]
    public string? TargetRefName { get; set; }

    /// <summary>
    /// The status of the merge.
    /// </summary>
    [Required]
    [JsonPropertyName("mergeStatus")]
    public string? MergeStatus { get; set; }

    /// <summary>
    /// The identifier of the merge.
    /// </summary>
    [Required]
    [JsonPropertyName("mergeId")]
    public string? MergeId { get; set; }

    /// <summary>
    /// The URL for the Pull Request.
    /// </summary>
    [Required]
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
