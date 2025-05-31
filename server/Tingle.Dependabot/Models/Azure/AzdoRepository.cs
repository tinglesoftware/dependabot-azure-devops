using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Azure;

public class AzdoRepository : AzdoRepositoryReference
{
    /// <summary>The default branch of the repository.</summary>
    [JsonPropertyName("defaultBranch")]
    public string? DefaultBranch { get; set; } // not required because some repositories do not have default branches

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }

    [JsonPropertyName("isFork")]
    public bool IsFork { get; set; }
}

public class AzdoRepositoryReference
{
    /// <summary>The unique identifier of the repository.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>The name of the repository.</summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>The details about the project which owns the repository.</summary>
    [JsonPropertyName("project")]
    public required AzdoProjectReference Project { get; set; }
}

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

[JsonConverter(typeof(JsonStringEnumMemberConverter<AzdoRepositoryItemType>))]
public enum AzdoRepositoryItemType
{
    [EnumMember(Value = "bad")] Bad,
    [EnumMember(Value = "blob")] Blob,
    [EnumMember(Value = "commit")] Commit,
    [EnumMember(Value = "ext2")] Ext2,
    [EnumMember(Value = "ofsDelta")] OfsDelta,
    [EnumMember(Value = "refDelta")] RefDelta,
    [EnumMember(Value = "tree")] Tree,
    [EnumMember(Value = "tag")] Tag,
}

public record AzdoCommitRef(
    [property: JsonPropertyName("commitId")] string CommitId);
