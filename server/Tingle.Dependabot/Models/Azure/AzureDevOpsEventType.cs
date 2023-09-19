using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

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
