using System.Runtime.Serialization;

namespace Tingle.Dependabot.Models;

public enum MergeStrategy
{
    [EnumMember(Value = "noFastForward")]
    NoFastForward = 0,

    [EnumMember(Value = "rebase")]
    Rebase = 1,

    [EnumMember(Value = "rebaseMerge")]
    RebaseMerge = 2,

    [EnumMember(Value = "squash")]
    Squash = 3,
}
