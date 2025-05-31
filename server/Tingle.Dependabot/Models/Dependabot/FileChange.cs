namespace Tingle.Dependabot.Models.Azure;

public record FileChange(VersionControlChangeType ChangeType, string Path, string? Content, string? Encoding);

public enum VersionControlChangeType
{
    None = 0,
    Add = 1,
    Edit = 2,
    Encoding = 4,
    Rename = 8,
    Delete = 16,
    Undelete = 32,
    Branch = 64,
    Merge = 128,
    Lock = 256,
    Rollback = 512,
    SourceRename = 1024,
    TargetRename = 2048,
    Property = 4096,
    All = 8191
}
