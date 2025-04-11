using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Tingle.Extensions.Primitives.Converters;

namespace Tingle.Dependabot.Models.Azure;

[JsonConverter(typeof(JsonStringEnumMemberConverter<AzdoProjectVisibility>))]
public enum AzdoProjectVisibility
{

    [EnumMember(Value = "private")]
    Private,

    [EnumMember(Value = "organization")]
    Organization,

    [EnumMember(Value = "public")]
    Public,

    [EnumMember(Value = "systemPrivate")]
    SystemPrivate,
}
