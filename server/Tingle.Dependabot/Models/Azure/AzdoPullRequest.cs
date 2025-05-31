using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Azure;

public class AzdoPullRequest
{
    /// <summary>Details about the repository.</summary>
    [JsonPropertyName("repository")]
    public required AzdoRepositoryReference Repository { get; set; }

    /// <summary>The identifier of the Pull Request.</summary>
    [JsonPropertyName("pullRequestId")]
    public required int PullRequestId { get; set; }

    /// <summary>The status of the Pull Request.</summary>
    [JsonPropertyName("status")]
    public required string Status { get; set; }

    /// <summary>The title of the Pull Request.</summary>
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    /// <summary>
    /// The branch of the repository from which the changes are picked from in the Pull Request.
    /// </summary>
    /// <example>refs/heads/feature/my-feature</example>
    [JsonPropertyName("sourceRefName")]
    public required string SourceRefName { get; set; }

    /// <summary>The branch of the repository to which the merge shall be done.</summary>
    /// <example>refs/heads/main</example>
    [JsonPropertyName("targetRefName")]
    public required string TargetRefName { get; set; }

    /// <summary>The status of the merge.</summary>
    [JsonPropertyName("mergeStatus")]
    public required string MergeStatus { get; set; }

    /// <summary>The identifier of the merge.</summary>
    [JsonPropertyName("mergeId")]
    public required string MergeId { get; set; }

    /// <summary>The commit at the head of the source branch at the time of the last pull request merge.</summary>
    [JsonPropertyName("lastMergeSourceCommit")]
    public required AzdoPullRequestLastMergeSourceCommit LastMergeSourceCommit { get; set; }

    /// <summary>The URL for the Pull Request.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}

/// <param name="CommitId">The URL for the Pull Request.</param>
/// <param name="Url">The URL for the Pull Request.</param>
public record AzdoPullRequestLastMergeSourceCommit(
    [property: JsonPropertyName("commitId")] string CommitId,
    [property: JsonPropertyName("url")] string Url);

public class AzdoPullRequestProperties : Dictionary<string, AzdoProperty> { }

public record AzdoPullRequestCommentThreadCreate(
    [property: JsonPropertyName("status")] AzdoPullRequestCommentThreadStatus Status,
    [property: JsonPropertyName("comments")] List<AzdoPullRequestCommentThreadComment> Comments);

public record AzdoPullRequestCommentThread(
    [property: JsonPropertyName("id")] string Id);

public record AzdoPullRequestCommentThreadComment(
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("commentType")] AzdoPullRequestCommentThreadCommentType CommentType,
    [property: JsonPropertyName("author")] AzdoIdentity Author);

[JsonConverter(typeof(JsonStringEnumMemberConverter<AzdoPullRequestCommentThreadStatus>))]
public enum AzdoPullRequestCommentThreadStatus
{
    [EnumMember(Value = "active")] Active,
    [EnumMember(Value = "byDesign")] ByDesign,
    [EnumMember(Value = "closed")] Closed,
    [EnumMember(Value = "fixed")] Fixed,
    [EnumMember(Value = "pending")] Pending,
    [EnumMember(Value = "unknown")] Unknown,
    [EnumMember(Value = "wontFix")] WontFix,
}

[JsonConverter(typeof(JsonStringEnumMemberConverter<AzdoPullRequestCommentThreadCommentType>))]
public enum AzdoPullRequestCommentThreadCommentType
{
    [EnumMember(Value = "codeChange")] CodeChange,
    [EnumMember(Value = "system")] System,
    [EnumMember(Value = "text")] Text,
    [EnumMember(Value = "unknown")] Unknown,
}

public record AzdoRefUpdate(
    [property: JsonPropertyName("isLocked")] bool IsLocked,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("newObjectId")] string NewObjectId,
    [property: JsonPropertyName("oldObjectId")] string OldObjectId);

/// <param name="Success">True if the ref update succeeded, false otherwise.</param>
public record AzdoRefUpdateResult(
    [property: JsonPropertyName("success")] bool Success);
