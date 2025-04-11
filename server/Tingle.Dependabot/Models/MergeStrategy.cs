using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models;

[JsonConverter(typeof(JsonStringEnumMemberConverter<MergeStrategy>))]
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
