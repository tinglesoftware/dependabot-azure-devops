using System.Text.Json.Serialization;

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

    /// <summary>The URL for the Pull Request.</summary>
    [JsonPropertyName("url")]
    public required string Url { get; set; }
}

public class AzdoPullRequestProperties : Dictionary<string, AzdoProperty> { }
