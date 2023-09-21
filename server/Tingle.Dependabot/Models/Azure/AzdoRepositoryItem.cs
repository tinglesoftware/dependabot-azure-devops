using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Tingle.Dependabot.Models.Azure;

public class AzdoRepositoryItem
{
    [JsonPropertyName("objectId")]
    public required string ObjectId { get; set; }

    [JsonPropertyName("originalObjectId")]
    public string? OriginalObjectId { get; set; }

    [JsonPropertyName("gitObjectType")]
    public required AzdoRepositoryItemType GitObjectType { get; set; }

    [JsonPropertyName("commitId")]
    public required string CommitId { get; set; }

    [JsonPropertyName("latestProcessedChange")]
    public required AzdoCommitRef LatestProcessedChange { get; set; }

    [JsonPropertyName("path")]
    public required string Path { get; set; }

    [JsonPropertyName("isFolder")]
    public bool IsFolder { get; set; }

    [JsonPropertyName("content")]
    public required string Content { get; set; }

    [JsonPropertyName("isSymLink")]
    public bool IsSymbolicLink { get; set; }
}

[JsonConverter(typeof(JsonStringEnumMemberConverter))]
public enum AzdoRepositoryItemType
{
    [EnumMember(Value = "bad")]
    Bad,

    [EnumMember(Value = "blob")]
    Blob,

    [EnumMember(Value = "commit")]
    Commit,

    [EnumMember(Value = "ext2")]
    Ext2,

    [EnumMember(Value = "ofsDelta")]
    OfsDelta,

    [EnumMember(Value = "refDelta")]
    RefDelta,

    [EnumMember(Value = "tree")]
    Tree,

    [EnumMember(Value = "tag")]
    Tag,
}

public class AzdoCommitRef
{
    [JsonPropertyName("commitId")]
    public required string CommitId { get; set; }
}
