using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot;

public class AzureDevOpsEvent
{
    [Required]
    [JsonPropertyName("subscriptionId")]
    public string? SubscriptionId { get; set; }

    [Required]
    [JsonPropertyName("notificationId")]
    public int NotificationId { get; set; }

    [Required]
    [JsonPropertyName("eventType")]
    public AzureDevOpsEventType? EventType { get; set; }

    [Required]
    [JsonPropertyName("resource")]
    public JsonObject? Resource { get; set; }
}

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

public class AzureDevOpsEventPullRequestCommentEventResource
{
    [Required]
    [JsonPropertyName("comment")]
    public AzureDevOpsEventCommentResource? Comment { get; set; }

    [Required]
    [JsonPropertyName("pullRequest")]
    public AzureDevOpsEventPullRequestResource? PullRequest { get; set; }
}

public class AzureDevOpsEventCommentResource
{
    [Required]
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("parentCommentId")]
    public int? ParentCommentId { get; set; }

    [Required]
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("commentType")]
    public string? CommentType { get; set; }

    [Required]
    [JsonPropertyName("publishedDate")]
    public DateTimeOffset? PublishedDate { get; set; }
}

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

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum AzureDevOpsEventType
{
    /// <summary>Code pushed</summary>
    /// <remarks>Code is pushed to a Git repository.</remarks>
    [EnumMember(Value = "git.push")]
    GitPush,

    /// <summary>Pull request updated</summary>
    /// <remarks>
    /// Pull request is updated – status, review list, reviewer vote
    /// changed or the source branch is updated with a push.
    /// </remarks>
    [EnumMember(Value = "git.pullrequest.updated")]
    GitPullRequestUpdated,

    /// <summary>Pull request merge attempted</summary>
    /// <remarks>Pull request - Branch merge attempted.</remarks>
    [EnumMember(Value = "git.pullrequest.merged")]
    GitPullRequestMerged,

    /// <summary>Pull request commented on</summary>
    /// <remarks>Comments are added to a pull request.</remarks>
    [EnumMember(Value = "ms.vss-code.git-pullrequest-comment-event")]
    GitPullRequestCommentEvent,
}
