using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Azure;

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

[JsonConverter(typeof(JsonStringEnumMemberConverter<AzureDevOpsEventType>))]
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

public class AzureDevOpsEventCodePushResource
{
    /// <summary>List of updated references.</summary>
    [Required]
    [JsonPropertyName("refUpdates")]
    public List<AzureDevOpsEventRefUpdate>? RefUpdates { get; set; }

    /// <summary>Details about the repository.</summary>
    [JsonPropertyName("repository")]
    public required AzdoRepository Repository { get; set; }
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

public class AzureDevOpsEventPullRequestCommentEventResource
{
    [Required]
    [JsonPropertyName("comment")]
    public AzureDevOpsEventCommentResource? Comment { get; set; }

    [Required]
    [JsonPropertyName("pullRequest")]
    public AzdoPullRequest? PullRequest { get; set; }
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
