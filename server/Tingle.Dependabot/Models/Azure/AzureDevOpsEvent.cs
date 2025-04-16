using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Azure;

public record AzureDevOpsEvent(
    [property: Required][property: JsonPropertyName("subscriptionId")] string? SubscriptionId,
    [property: Required][property: JsonPropertyName("notificationId")] int NotificationId,
    [property: Required][property: JsonPropertyName("eventType")] AzureDevOpsEventType? EventType,
    [property: Required][property: JsonPropertyName("resource")] JsonObject? Resource);

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

/// <param name="RefUpdates">List of updated references.</param>
/// <param name="Repository">Details about the repository.</param>
public record AzureDevOpsEventCodePushResource(
    [property: JsonPropertyName("refUpdates")] List<AzureDevOpsEventRefUpdate> RefUpdates,
    [property: JsonPropertyName("repository")] AzdoRepository Repository);

public record AzureDevOpsEventCommentResource(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("parentCommentId")] int? ParentCommentId,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("commentType")] string? CommentType,
    [property: JsonPropertyName("publishedDate")] DateTimeOffset PublishedDate);

public record AzureDevOpsEventPullRequestCommentEventResource(
    [property: JsonPropertyName("comment")] AzureDevOpsEventCommentResource Comment,
    [property: JsonPropertyName("pullRequest")] AzdoPullRequest PullRequest);

public record AzureDevOpsEventRefUpdate(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("oldObjectId")] string? OldObjectId,
    [property: JsonPropertyName("newObjectId")] string? NewObjectId);
