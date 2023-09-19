using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzureDevOpsEventPullRequestCommentEventResource
{
    [Required]
    [JsonPropertyName("comment")]
    public AzureDevOpsEventCommentResource? Comment { get; set; }

    [Required]
    [JsonPropertyName("pullRequest")]
    public AzureDevOpsEventPullRequestResource? PullRequest { get; set; }
}
