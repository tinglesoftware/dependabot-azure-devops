using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

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
